using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Utils;

namespace Gatehouse.Accounts;

internal sealed class SmtpEmailDelivery(IOptions<EmailOptions> options) : IEmailDelivery
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var settings = options.Value;

        using var mime = BuildMessage(message, settings);
        using var client = new SmtpClient();
        var security = settings.Smtp.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(settings.Smtp.Host, settings.Smtp.Port, security, ct);
        if (settings.Smtp.Username.Length > 0)
        {
            await client.AuthenticateAsync(settings.Smtp.Username, settings.Smtp.Password, ct);
        }

        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(quit: true, ct);
    }

    internal static MimeMessage BuildMessage(EmailMessage message, EmailOptions settings)
    {
        var mime = new MimeMessage();
        var (fromName, fromAddress) = message.From ?? new EmailAddress(settings.FromName, settings.FromAddress);
        var from = new MailboxAddress(fromName, fromAddress);
        mime.From.Add(from);

        // MimeKit's default Message-ID ends in this machine's host name, which in Docker is the container's id
        // (e.g. "@2b9359c114d8"). Spam filters score a Message-ID without a real domain against the email, so it
        // gets the sender's domain instead.
        mime.MessageId = MimeUtils.GenerateMessageId(from.Domain);

        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        if (message.Headers is not null)
        {
            foreach (var (name, value) in message.Headers)
            {
                mime.Headers.Add(name, value);
            }
        }

        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();
        return mime;
    }
}
