namespace Gatehouse.Accounts.Tests;

/// <summary>What the tests can switch on the hooks, and what the hooks recorded. One per test host.</summary>
public sealed class TestHooksState
{
    public bool AllowNewAccounts { get; set; } = true;

    public bool ThrowOnSignedIn { get; set; }

    public List<(string? Email, bool NewAccount)> SignedIn { get; } = [];
}

public sealed class TestHooks(TestHooksState state) : ExternalAccountHooks<TestUser>
{
    public override TestUser CreateUser(ExternalAccount account) =>
        new() { UserName = account.Email, Email = account.Email, DisplayName = account.Name };

    public override ValueTask<bool> AllowNewAccountAsync(ExternalAccount account, CancellationToken ct) =>
        ValueTask.FromResult(state.AllowNewAccounts);

    public override ValueTask<bool> CanSignInAsync(TestUser user, CancellationToken ct) =>
        ValueTask.FromResult(!user.IsDisabled);

    public override ValueTask OnSignedInAsync(TestUser user, bool newAccount, CancellationToken ct)
    {
        if (state.ThrowOnSignedIn)
        {
            throw new InvalidOperationException("The hook failed.");
        }

        state.SignedIn.Add((user.Email, newAccount));
        return ValueTask.CompletedTask;
    }
}
