namespace Gatehouse.Accounts;

/// <summary>A sender's name and address.</summary>
/// <param name="Name">The display name.</param>
/// <param name="Address">The email address.</param>
public sealed record EmailAddress(string Name, string Address);

/// <summary>A finished email, ready to send.</summary>
/// <param name="To">The recipient's address.</param>
/// <param name="Subject">The subject line.</param>
/// <param name="HtmlBody">The HTML body.</param>
/// <param name="TextBody">The plain-text body.</param>
public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody)
{
    /// <summary>The sender. Null = <c>Email:FromName</c> and <c>Email:FromAddress</c>.</summary>
    public EmailAddress? From { get; init; }

    /// <summary>Extra headers, for example <c>List-Unsubscribe</c>. Null = none.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
}

/// <summary>
/// Sends a finished email the way <c>Email:Delivery</c> says: over SMTP, to the log (Development), or not at all.
/// The package registers it, so an app can send its own emails (newsletters, alerts, notices) through the same settings.
/// </summary>
public interface IEmailDelivery
{
    /// <summary>Sends the email.</summary>
    /// <param name="message">The email.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the email has been handed over.</returns>
    Task SendAsync(EmailMessage message, CancellationToken ct);
}
