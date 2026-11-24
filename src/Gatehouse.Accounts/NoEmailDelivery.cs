using Microsoft.Extensions.Logging;

namespace Gatehouse.Accounts;

/// <summary>Email:Delivery is None: nothing is sent. A warning is logged per email (without the address).</summary>
internal sealed partial class NoEmailDelivery(ILogger<NoEmailDelivery> logger) : IEmailDelivery
{
    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        LogNotSent(logger);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email delivery is None: an email was not sent")]
    private static partial void LogNotSent(ILogger logger);
}
