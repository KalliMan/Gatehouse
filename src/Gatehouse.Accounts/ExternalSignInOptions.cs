namespace Gatehouse.Accounts;

/// <summary>What to do when someone signs in with a provider and an account with the same email exists but its email was never confirmed.</summary>
public enum UnconfirmedAccountRule
{
    /// <summary>
    /// The provider-verified owner gets the account and its password is removed. A stranger may have registered the
    /// address first, so nothing they set up on the account survives.
    /// </summary>
    TakeOver,

    /// <summary>The sign-in is refused with the "email in use" error. The owner confirms the email first.</summary>
    Refuse,
}

/// <summary>Settings of the provider sign-in. Set them in code through the callback of <c>AddGatehouseExternalSignIn</c>.</summary>
public sealed class ExternalSignInOptions
{
    /// <summary>The page the browser goes to when a sign-in fails, with <c>?error=...</c>. Must start with '/'. Default "/login".</summary>
    public string LoginPath { get; set; } = "/login";

    /// <summary>
    /// The start of the path each provider returns to, followed by the lower-case provider name (for example
    /// "/api/auth/signin-google"). Must start with '/'. Register these addresses with the providers. Default "/api/auth/signin-".
    /// </summary>
    public string CallbackPathPrefix { get; set; } = "/api/auth/signin-";

    /// <summary>What to do with an existing account with the same email whose email is unconfirmed. Default <see cref="UnconfirmedAccountRule.TakeOver"/>.</summary>
    public UnconfirmedAccountRule UnconfirmedAccountWithSameEmail { get; set; } = UnconfirmedAccountRule.TakeOver;

    /// <summary>
    /// True: a new account made from a provider that does not verify emails (Microsoft, Facebook) gets a confirmed
    /// email anyway. False (default): its email stays unconfirmed and the user must confirm it.
    /// </summary>
    public bool ConfirmUnverifiedEmailOfNewAccounts { get; set; }
}
