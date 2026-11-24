using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gatehouse.Accounts;

/// <summary>
/// Development only: writes the email, including recipient and link, to the log instead of sending it. Startup
/// fails if this is configured outside Development. This is the one place where an address and a link are logged.
/// </summary>
internal sealed partial class LogEmailDelivery(IOptions<EmailOptions> options, ILogger<LogEmailDelivery> logger)
    : IEmailDelivery
{
    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var (fromName, fromAddress) = message.From ?? new EmailAddress(options.Value.FromName, options.Value.FromAddress);
        var headers = message.Headers is null
            ? ""
            : string.Concat(message.Headers.Select(h => $"{h.Key}: {h.Value}\n"));
        LogEmail(logger, message.To, $"{fromName} <{fromAddress}>", message.Subject, headers + message.TextBody);
        return Task.CompletedTask;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Email (not sent, Development) to {To} from {From}: {Subject}\n{Body}")]
    private static partial void LogEmail(ILogger logger, string to, string from, string subject, string body);
}
