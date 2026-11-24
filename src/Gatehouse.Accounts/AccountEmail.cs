namespace Gatehouse.Accounts;

/// <summary>The kinds of account email the package asks the app to write.</summary>
public enum AccountEmailKind
{
    /// <summary>Confirm the email address of a new account.</summary>
    ConfirmEmail,

    /// <summary>Confirm a new email address that replaces the account's current one.</summary>
    ConfirmEmailChange,

    /// <summary>Set a new password after "forgot password".</summary>
    ResetPassword,
}

/// <summary>What the package knows about an account email: its kind, the address and the frontend link.</summary>
/// <param name="Kind">The kind of email.</param>
/// <param name="To">The address to send to (for an email change: the new address).</param>
/// <param name="Link">The absolute frontend link with its query string.</param>
public sealed record AccountEmail(AccountEmailKind Kind, string To, string Link);

/// <summary>
/// The app's part of account emails: it writes the subject and the bodies (in the user's language, with the app's
/// design). One instance is created per email, in its own scope, so it can take scoped services in its constructor;
/// <c>IHttpContextAccessor</c> still gives the request when there is one.
/// </summary>
/// <typeparam name="TUser">The Identity user class.</typeparam>
public interface IAccountEmailComposer<TUser> where TUser : class
{
    /// <summary>Writes the email.</summary>
    /// <param name="user">The account the email is about.</param>
    /// <param name="email">The kind, the address and the link.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The finished email, or null to send nothing (for example when the app's daily limit is reached).</returns>
    Task<EmailMessage?> ComposeAsync(TUser user, AccountEmail email, CancellationToken ct);
}
