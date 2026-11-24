namespace Gatehouse.Accounts.Tests;

public sealed class SmtpEmailDeliveryTests
{
    private static readonly EmailOptions Settings = new() { FromAddress = "noreply@example.com", FromName = "The App" };

    private static EmailMessage Message() => new("ann@example.org", "Hello", "<p>Hi</p>", "Hi");

    [Fact]
    public void The_default_sender_comes_from_the_options()
    {
        using var mime = SmtpEmailDelivery.BuildMessage(Message(), Settings);

        var from = Assert.Single(mime.From.Mailboxes);
        Assert.Equal("noreply@example.com", from.Address);
        Assert.Equal("The App", from.Name);
        Assert.Equal("ann@example.org", Assert.Single(mime.To.Mailboxes).Address);
        Assert.Equal("Hello", mime.Subject);
    }

    [Fact]
    public void The_sender_of_the_message_wins()
    {
        var message = Message() with { From = new EmailAddress("News", "news@other.example") };

        using var mime = SmtpEmailDelivery.BuildMessage(message, Settings);

        var from = Assert.Single(mime.From.Mailboxes);
        Assert.Equal("news@other.example", from.Address);
        Assert.Equal("News", from.Name);
    }

    [Fact]
    public void Extra_headers_are_added()
    {
        var message = Message() with
        {
            Headers = new Dictionary<string, string> { ["List-Unsubscribe"] = "<https://app.example/unsubscribe>" },
        };

        using var mime = SmtpEmailDelivery.BuildMessage(message, Settings);

        Assert.Equal("<https://app.example/unsubscribe>", mime.Headers["List-Unsubscribe"]);
    }

    [Fact]
    public void The_message_id_uses_the_domain_of_the_sender()
    {
        using var mime = SmtpEmailDelivery.BuildMessage(Message(), Settings);

        Assert.EndsWith("@example.com", mime.MessageId);
    }

    [Fact]
    public void Both_the_html_and_the_text_body_are_there()
    {
        using var mime = SmtpEmailDelivery.BuildMessage(Message(), Settings);

        Assert.Equal("<p>Hi</p>", mime.HtmlBody);
        Assert.Equal("Hi", mime.TextBody);
    }
}
