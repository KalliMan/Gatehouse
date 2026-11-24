namespace Gatehouse.Accounts.Tests;

/// <summary>What the tests can switch on the composer. One per test host.</summary>
public sealed class ComposerState
{
    public bool ReturnNull { get; set; }

    public bool Throw { get; set; }
}

/// <summary>Writes the simplest email: the subject is the kind, the text is the link.</summary>
public sealed class TestComposer(ComposerState state) : IAccountEmailComposer<TestUser>
{
    public Task<EmailMessage?> ComposeAsync(TestUser user, AccountEmail email, CancellationToken ct)
    {
        if (state.Throw)
        {
            throw new InvalidOperationException("Composing failed.");
        }

        return Task.FromResult<EmailMessage?>(state.ReturnNull
            ? null
            : new EmailMessage(email.To, email.Kind.ToString(), $"<p>{email.Link}</p>", email.Link));
    }
}
