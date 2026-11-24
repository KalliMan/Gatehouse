namespace Gatehouse.Accounts;

/// <summary>The outcome of a provider sign-in; anything but <see cref="Success"/> becomes an error code on the sign-in page.</summary>
internal enum ExternalSignInResult
{
    Success,
    NoEmail,
    EmailInUse,
    LockedOut,
    Disabled,
    SignUpClosed,
    Failed,
}
