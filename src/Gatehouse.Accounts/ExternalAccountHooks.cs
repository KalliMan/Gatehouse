namespace Gatehouse.Accounts;

/// <summary>
/// The app's part of provider sign-in. Derive from it and pass the class to <c>AddGatehouseExternalSignIn</c>. It is
/// created once per request, so it can take scoped services (a database context, a clock) in its constructor.
/// </summary>
/// <typeparam name="TUser">The Identity user class.</typeparam>
public abstract class ExternalAccountHooks<TUser> where TUser : class
{
    /// <summary>
    /// Makes a new, unsaved user for a first-time sign-in: set the user name, the email and the app's own fields.
    /// The package saves it and confirms its email through <c>UserManager</c>, so do not set the confirmation here.
    /// </summary>
    /// <param name="account">What the provider told us.</param>
    /// <returns>The new user.</returns>
    public abstract TUser CreateUser(ExternalAccount account);

    /// <summary>Whether a new account may be made (return false when sign-up is closed). Default true.</summary>
    /// <param name="account">What the provider told us.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True to go on and create the account.</returns>
    public virtual ValueTask<bool> AllowNewAccountAsync(ExternalAccount account, CancellationToken ct) =>
        ValueTask.FromResult(true);

    /// <summary>Whether an existing account may sign in (return false for a disabled account). Default true.</summary>
    /// <param name="user">The account.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>False to refuse the sign-in with the "disabled" error.</returns>
    public virtual ValueTask<bool> CanSignInAsync(TUser user, CancellationToken ct) => ValueTask.FromResult(true);

    /// <summary>
    /// Called after a successful sign-in, for example to count it or to store the last sign-in time. An exception
    /// thrown here is logged and does not stop the sign-in. Default: nothing.
    /// </summary>
    /// <param name="user">The signed-in user.</param>
    /// <param name="newAccount">True when the account was just created.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the app is done.</returns>
    public virtual ValueTask OnSignedInAsync(TUser user, bool newAccount, CancellationToken ct) => ValueTask.CompletedTask;
}
