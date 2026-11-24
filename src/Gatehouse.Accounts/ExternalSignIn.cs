using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gatehouse.Accounts;

/// <summary>
/// Sign-in with a provider: start the redirect, and on the way back find, link or create the account and sign in
/// with the normal session cookie.
/// </summary>
internal sealed partial class ExternalSignIn<TUser>(
    SignInManager<TUser> signInManager,
    ExternalAccountHooks<TUser> hooks,
    IOptions<ExternalSignInOptions> options,
    ILogger<ExternalSignIn<TUser>> logger) where TUser : class
{
    private UserManager<TUser> Users => signInManager.UserManager;

    public AuthenticationProperties CreateChallenge(string provider, string callbackUrl) =>
        signInManager.ConfigureExternalAuthenticationProperties(provider, callbackUrl);

    /// <summary>Called when the provider has sent the user back. Signs in on success.</summary>
    public async Task<ExternalSignInResult> CompleteAsync(CancellationToken ct)
    {
        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            return ExternalSignInResult.Failed;
        }

        try
        {
            return await SignInAsync(info, ct);
        }
        finally
        {
            // The short-lived cookie that carried the provider's answer is no longer needed.
            await signInManager.Context.SignOutAsync(IdentityConstants.ExternalScheme);
        }
    }

    internal async Task<ExternalSignInResult> SignInAsync(ExternalLoginInfo info, CancellationToken ct)
    {
        var provider = info.LoginProvider;

        // 1. Already linked: the usual case after the first time.
        var linked = await Users.FindByLoginAsync(provider, info.ProviderKey);
        if (linked is not null)
        {
            return await SignInLinkedAsync(linked, info, ct);
        }

        // 2. The provider must send an email address.
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return ExternalSignInResult.NoEmail;
        }

        var verified = ExternalProviderSetup.IsEmailVerified(info);
        var name = info.Principal.FindFirstValue(ClaimTypes.Name)?.Trim();
        var account = new ExternalAccount(provider, email, string.IsNullOrEmpty(name) ? null : name, verified);

        // 3. An account with this email already exists.
        var user = await Users.FindByEmailAsync(email);
        if (user is not null)
        {
            // Linking gives whoever controls the provider account access to this account. That is safe only when the
            // provider has verified the email; otherwise anyone could take over an account by putting its email on a
            // provider account.
            if (!verified)
            {
                return ExternalSignInResult.EmailInUse;
            }
            if (!await hooks.CanSignInAsync(user, ct))
            {
                return ExternalSignInResult.Disabled;
            }
            if (await Users.IsLockedOutAsync(user))
            {
                return ExternalSignInResult.LockedOut;
            }

            if (!await Users.IsEmailConfirmedAsync(user))
            {
                if (options.Value.UnconfirmedAccountWithSameEmail == UnconfirmedAccountRule.Refuse)
                {
                    return ExternalSignInResult.EmailInUse;
                }

                // Nobody has proved they own this address yet, so the password on the account may be a stranger's who
                // registered it first. The provider has verified the address: its owner keeps the account and the
                // password goes. RemovePasswordAsync also renews the security stamp.
                if (await Users.HasPasswordAsync(user)
                    && !await SucceededAsync(Users.RemovePasswordAsync(user), "remove the password of"))
                {
                    return ExternalSignInResult.Failed;
                }
                if (!await ConfirmEmailAsync(user))
                {
                    return ExternalSignInResult.Failed;
                }
            }
        }

        // 4. A new account.
        var newAccount = user is null;
        if (user is null)
        {
            if (!await hooks.AllowNewAccountAsync(account, ct))
            {
                return ExternalSignInResult.SignUpClosed;
            }

            user = hooks.CreateUser(account);
            if (!await SucceededAsync(Users.CreateAsync(user), "create"))
            {
                return ExternalSignInResult.Failed;
            }
            if ((verified || options.Value.ConfirmUnverifiedEmailOfNewAccounts) && !await ConfirmEmailAsync(user))
            {
                return ExternalSignInResult.Failed;
            }
        }

        // 5. Link the provider and sign in.
        if (!await SucceededAsync(Users.AddLoginAsync(user, info), "link"))
        {
            return ExternalSignInResult.Failed;
        }

        await signInManager.SignInAsync(user, isPersistent: true, provider);
        await NotifySignedInAsync(user, newAccount, ct);
        return ExternalSignInResult.Success;
    }

    private async Task<ExternalSignInResult> SignInLinkedAsync(TUser linked, ExternalLoginInfo info, CancellationToken ct)
    {
        if (!await hooks.CanSignInAsync(linked, ct))
        {
            return ExternalSignInResult.Disabled;
        }

        var result = await signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);
        if (result.Succeeded)
        {
            await NotifySignedInAsync(linked, newAccount: false, ct);
            return ExternalSignInResult.Success;
        }
        if (result.IsLockedOut)
        {
            return ExternalSignInResult.LockedOut;
        }
        if (result.IsNotAllowed)
        {
            // Identity refuses because the email isn't confirmed (required once email is switched on). That rule is
            // for passwords; here the provider vouches for the person, so a linked account may sign in.
            if (await Users.IsLockedOutAsync(linked))
            {
                return ExternalSignInResult.LockedOut;
            }

            await signInManager.SignInAsync(linked, isPersistent: true, info.LoginProvider);
            await NotifySignedInAsync(linked, newAccount: false, ct);
            return ExternalSignInResult.Success;
        }

        return ExternalSignInResult.Failed;
    }

    private async Task<bool> ConfirmEmailAsync(TUser user)
    {
        var token = await Users.GenerateEmailConfirmationTokenAsync(user);
        return await SucceededAsync(Users.ConfirmEmailAsync(user, token), "confirm the email of");
    }

    private async Task NotifySignedInAsync(TUser user, bool newAccount, CancellationToken ct)
    {
        try
        {
            await hooks.OnSignedInAsync(user, newAccount, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The sign-in itself worked. The exception type only: its message may hold personal data.
            LogSignedInHookFailed(logger, ex.GetType().Name);
        }
    }

    private async Task<bool> SucceededAsync(Task<IdentityResult> operation, string action)
    {
        var result = await operation;
        if (!result.Succeeded)
        {
            // Error codes only: Identity's descriptions can contain the email.
            LogAccountChangeFailed(logger, action, string.Join(", ", result.Errors.Select(e => e.Code)));
        }

        return result.Succeeded;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "External sign-in: could not {Action} the account ({ErrorCodes})")]
    private static partial void LogAccountChangeFailed(ILogger logger, string action, string errorCodes);

    [LoggerMessage(Level = LogLevel.Warning, Message = "External sign-in: the OnSignedIn hook failed ({ExceptionType})")]
    private static partial void LogSignedInHookFailed(ILogger logger, string exceptionType);
}
