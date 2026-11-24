using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gatehouse.Accounts;

/// <summary>
/// The emails Identity's endpoints send. Identity builds links to its own API endpoints; users get links to frontend
/// pages instead (<c>Email:ConfirmEmailPath</c>, <c>Email:ResetPasswordPath</c>), which call those endpoints and show
/// the result. The app's <see cref="IAccountEmailComposer{TUser}"/> writes the email; <see cref="IEmailDelivery"/> sends it.
/// A failed send is logged, not thrown: the account change already happened, and the user can ask for the email again.
/// </summary>
/// <typeparam name="TUser">The Identity user class.</typeparam>
/// <param name="scopes">Makes the scope in which the composer is resolved.</param>
/// <param name="delivery">Sends the finished email.</param>
/// <param name="options">The email settings.</param>
/// <param name="logger">The logger.</param>
public sealed partial class AccountEmailSender<TUser>(
    IServiceScopeFactory scopes,
    IEmailDelivery delivery,
    IOptions<EmailOptions> options,
    ILogger<AccountEmailSender<TUser>> logger) : IEmailSender<TUser> where TUser : class
{
    /// <summary>Called by Identity's endpoints after sign-up and after a change of email.</summary>
    /// <param name="user">The account.</param>
    /// <param name="email">The address to send to.</param>
    /// <param name="confirmationLink">
    /// The HTML-encoded link to Identity's <c>confirmEmail</c> endpoint, with <c>userId</c>, <c>code</c> and, for an
    /// email change, <c>changedEmail</c>. Only its query string is used.
    /// </param>
    /// <returns>A task that completes when the email was sent or the failure was logged. It never throws.</returns>
    public Task SendConfirmationLinkAsync(TUser user, string email, string confirmationLink) =>
        SendAsync(user, AccountEmailKind.ConfirmEmail, () =>
        {
            var query = QueryHelpers.ParseQuery(new Uri(WebUtility.HtmlDecode(confirmationLink)).Query);
            // The sign-up link has no changedEmail; indexing a missing key throws.
            var changedEmail = query.TryGetValue("changedEmail", out var changed) ? changed.ToString() : null;
            return ConfirmationEmail(email, query["userId"].ToString(), query["code"].ToString(), changedEmail);
        });

    /// <summary>Called by Identity's <c>forgotPassword</c> endpoint when it sends a link instead of a code.</summary>
    /// <param name="user">The account.</param>
    /// <param name="email">The address to send to.</param>
    /// <param name="resetLink">The HTML-encoded link, used as it is.</param>
    /// <returns>A task that completes when the email was sent or the failure was logged. It never throws.</returns>
    public Task SendPasswordResetLinkAsync(TUser user, string email, string resetLink) =>
        SendAsync(user, AccountEmailKind.ResetPassword, () =>
            new AccountEmail(AccountEmailKind.ResetPassword, email, WebUtility.HtmlDecode(resetLink)));

    /// <summary>Called by Identity's <c>forgotPassword</c> endpoint.</summary>
    /// <param name="user">The account.</param>
    /// <param name="email">The address to send to.</param>
    /// <param name="resetCode">The code the reset page sends back to <c>resetPassword</c>.</param>
    /// <returns>A task that completes when the email was sent or the failure was logged. It never throws.</returns>
    public Task SendPasswordResetCodeAsync(TUser user, string email, string resetCode) =>
        SendAsync(user, AccountEmailKind.ResetPassword, () =>
        {
            var link = QueryHelpers.AddQueryString(
                FrontendUrl(options.Value.ResetPasswordPath),
                new Dictionary<string, string?> { ["email"] = email, ["code"] = resetCode });
            return new AccountEmail(AccountEmailKind.ResetPassword, email, link);
        });

    /// <summary>For an app's own register or change-email endpoint, which makes the confirmation code itself.</summary>
    /// <param name="user">The account.</param>
    /// <param name="email">The address to send to (for an email change: the new address).</param>
    /// <param name="userId">The account's id, as Identity's <c>confirmEmail</c> endpoint expects it.</param>
    /// <param name="code">The confirmation code, Base64Url-encoded as Identity's endpoints expect.</param>
    /// <param name="changedEmail">The new address for an email change; null or empty for a new account.</param>
    /// <returns>A task that completes when the email was sent or the failure was logged. It never throws.</returns>
    public Task SendConfirmationAsync(TUser user, string email, string userId, string code, string? changedEmail = null) =>
        SendAsync(user, AccountEmailKind.ConfirmEmail, () => ConfirmationEmail(email, userId, code, changedEmail));

    private AccountEmail ConfirmationEmail(string email, string userId, string code, string? changedEmail)
    {
        var parameters = new Dictionary<string, string?> { ["userId"] = userId, ["code"] = code };
        var isEmailChange = !string.IsNullOrEmpty(changedEmail);
        if (isEmailChange)
        {
            parameters["changedEmail"] = changedEmail;
        }

        return new AccountEmail(
            isEmailChange ? AccountEmailKind.ConfirmEmailChange : AccountEmailKind.ConfirmEmail,
            email,
            QueryHelpers.AddQueryString(FrontendUrl(options.Value.ConfirmEmailPath), parameters));
    }

    private string FrontendUrl(string path) => $"{options.Value.FrontendBaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";

    private async Task SendAsync(TUser user, AccountEmailKind kind, Func<AccountEmail> build)
    {
        try
        {
            var email = build();
            kind = email.Kind;

            // One scope per email: the sender is a singleton (MapIdentityApi resolves it once), the composer may not be.
            await using var scope = scopes.CreateAsyncScope();
            var composer = scope.ServiceProvider.GetRequiredService<IAccountEmailComposer<TUser>>();
            var message = await composer.ComposeAsync(user, email, CancellationToken.None);
            if (message is null)
            {
                LogNotSent(logger, kind);
                return;
            }

            await delivery.SendAsync(message, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // The kind and the exception type only: the recipient is personal data, and an SMTP server's answer in
            // the exception message often holds the address (e.g. "550 <name@example.com>: Recipient rejected").
            LogSendFailed(logger, kind, ex.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Account email ({Kind}) not sent: the composer returned none")]
    private static partial void LogNotSent(ILogger logger, AccountEmailKind kind);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sending the {Kind} email failed ({ExceptionType})")]
    private static partial void LogSendFailed(ILogger logger, AccountEmailKind kind, string exceptionType);
}
