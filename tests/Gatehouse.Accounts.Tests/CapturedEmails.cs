namespace Gatehouse.Accounts.Tests;

/// <summary>Takes the place of the real delivery: keeps what would have been sent.</summary>
public sealed class CapturedEmails : IEmailDelivery
{
    public List<EmailMessage> Sent { get; } = [];

    public bool Throw { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (Throw)
        {
            // Like a real SMTP server's answer: it names the recipient.
            throw new InvalidOperationException($"550 <{message.To}>: Recipient address rejected");
        }

        Sent.Add(message);
        return Task.CompletedTask;
    }
}
