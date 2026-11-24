using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using HostBuilder = Microsoft.Extensions.Hosting.Host;

namespace Gatehouse.Accounts.Tests;

public sealed class AccountEmailSenderTests
{
    private static readonly TestUser User = new() { Id = Guid.NewGuid(), UserName = "ann@example.com" };

    private sealed class Setup : IAsyncDisposable
    {
        public Setup(string baseUrl = "https://app.example")
        {
            var builder = HostBuilder.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Development" });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:FrontendBaseUrl"] = baseUrl,
            });
            builder.Logging.AddProvider(Logs);
            builder.Services.AddSingleton<ComposerState>();
            builder.Services.AddGatehouseAccountEmails<TestUser, TestComposer>(builder.Configuration);
            builder.Services.AddSingleton<IEmailDelivery>(Captured);
            Host = builder.Build();
        }

        public IHost Host { get; }

        public CapturedEmails Captured { get; } = new();

        public LogCapture Logs { get; } = new();

        public ComposerState Composer => Host.Services.GetRequiredService<ComposerState>();

        public AccountEmailSender<TestUser> Sender => Host.Services.GetRequiredService<AccountEmailSender<TestUser>>();

        public ValueTask DisposeAsync()
        {
            Host.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task A_confirmation_link_with_changedEmail_is_an_email_change()
    {
        await using var setup = new Setup();
        const string link = "https://api.example/api/auth/confirmEmail?userId=u1&amp;code=abc&amp;changedEmail=new@example.com";

        await setup.Sender.SendConfirmationLinkAsync(User, "new@example.com", link);

        var sent = Assert.Single(setup.Captured.Sent);
        Assert.Equal("ConfirmEmailChange", sent.Subject);
        Assert.Equal("new@example.com", sent.To);
        Assert.Equal("https://app.example/confirm-email?userId=u1&code=abc&changedEmail=new@example.com", sent.TextBody);
    }

    [Fact]
    public async Task A_confirmation_link_without_changedEmail_is_a_sign_up()
    {
        await using var setup = new Setup();
        const string link = "https://api.example/api/auth/confirmEmail?userId=u1&amp;code=abc";

        await setup.Sender.SendConfirmationLinkAsync(User, "ann@example.com", link);

        var sent = Assert.Single(setup.Captured.Sent);
        Assert.Equal("ConfirmEmail", sent.Subject);
        Assert.Equal("ann@example.com", sent.To);
        Assert.Equal("https://app.example/confirm-email?userId=u1&code=abc", sent.TextBody);
    }

    [Fact]
    public async Task SendConfirmationAsync_builds_the_link_from_its_arguments()
    {
        await using var setup = new Setup();

        await setup.Sender.SendConfirmationAsync(User, "ann@example.com", "u1", "a+b/c");
        await setup.Sender.SendConfirmationAsync(User, "new@example.com", "u1", "abc", "new@example.com");

        Assert.Equal(2, setup.Captured.Sent.Count);
        Assert.Equal("ConfirmEmail", setup.Captured.Sent[0].Subject);
        Assert.Equal("https://app.example/confirm-email?userId=u1&code=a%2Bb%2Fc", setup.Captured.Sent[0].TextBody);
        Assert.Equal("ConfirmEmailChange", setup.Captured.Sent[1].Subject);
        Assert.Equal("https://app.example/confirm-email?userId=u1&code=abc&changedEmail=new@example.com", setup.Captured.Sent[1].TextBody);
    }

    [Fact]
    public async Task A_reset_code_becomes_a_frontend_link()
    {
        await using var setup = new Setup();

        await setup.Sender.SendPasswordResetCodeAsync(User, "ann@example.com", "CODE");

        var sent = Assert.Single(setup.Captured.Sent);
        Assert.Equal("ResetPassword", sent.Subject);
        Assert.Equal("https://app.example/reset-password?email=ann@example.com&code=CODE", sent.TextBody);
    }

    [Fact]
    public async Task A_reset_link_is_html_decoded()
    {
        await using var setup = new Setup();

        await setup.Sender.SendPasswordResetLinkAsync(User, "ann@example.com", "https://app.example/r?a=1&amp;b=2");

        Assert.Equal("https://app.example/r?a=1&b=2", Assert.Single(setup.Captured.Sent).TextBody);
    }

    [Fact]
    public async Task A_composer_that_answers_null_sends_nothing()
    {
        await using var setup = new Setup();
        setup.Composer.ReturnNull = true;

        await setup.Sender.SendPasswordResetCodeAsync(User, "ann@example.com", "CODE");

        Assert.Empty(setup.Captured.Sent);
    }

    [Fact]
    public async Task A_failing_delivery_does_not_throw()
    {
        await using var setup = new Setup();
        setup.Captured.Throw = true;

        await setup.Sender.SendPasswordResetCodeAsync(User, "ann@example.com", "CODE");
        await setup.Sender.SendConfirmationAsync(User, "ann@example.com", "u1", "abc");

        Assert.Empty(setup.Captured.Sent);
    }

    [Fact]
    public async Task A_failed_send_never_logs_the_address()
    {
        await using var setup = new Setup();
        setup.Captured.Throw = true;

        await setup.Sender.SendPasswordResetCodeAsync(User, "ann@example.com", "CODE");

        var failure = Assert.Single(setup.Logs.Entries, e => e.Message.Contains("email failed", StringComparison.Ordinal));
        Assert.Contains("InvalidOperationException", failure.Message, StringComparison.Ordinal);
        Assert.Null(failure.Exception);
        Assert.DoesNotContain(setup.Logs.Entries, e => e.Message.Contains("ann@example.com", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_failing_composer_does_not_throw()
    {
        await using var setup = new Setup();
        setup.Composer.Throw = true;

        await setup.Sender.SendPasswordResetCodeAsync(User, "ann@example.com", "CODE");

        Assert.Empty(setup.Captured.Sent);
    }

    [Fact]
    public async Task A_garbled_confirmation_link_does_not_throw()
    {
        await using var setup = new Setup();

        await setup.Sender.SendConfirmationLinkAsync(User, "ann@example.com", "not a link");

        Assert.Empty(setup.Captured.Sent);
    }

    [Fact]
    public async Task A_trailing_slash_on_the_frontend_address_makes_no_double_slash()
    {
        await using var setup = new Setup(baseUrl: "https://app.example/");

        await setup.Sender.SendPasswordResetCodeAsync(User, "ann@example.com", "CODE");

        Assert.StartsWith("https://app.example/reset-password?", Assert.Single(setup.Captured.Sent).TextBody);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Identitys_email_sender_is_ours_whether_identity_is_registered_before_or_after(bool identityFirst)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ComposerState>();
        services.AddSingleton<IHostEnvironment>(new FakeEnvironment());   // the email options check it
        if (identityFirst)
        {
            AddIdentityApi(services);
        }

        services.AddGatehouseAccountEmails<TestUser, TestComposer>(new ConfigurationBuilder().Build());
        services.AddSingleton<IEmailDelivery>(new CapturedEmails());
        if (!identityFirst)
        {
            AddIdentityApi(services);
        }

        using var provider = services.BuildServiceProvider();

        Assert.IsType<AccountEmailSender<TestUser>>(provider.GetRequiredService<IEmailSender<TestUser>>());
        Assert.Same(
            provider.GetRequiredService<AccountEmailSender<TestUser>>(),
            provider.GetRequiredService<IEmailSender<TestUser>>());
    }

    private static void AddIdentityApi(IServiceCollection services) =>
        services.AddIdentityApiEndpoints<TestUser>().AddEntityFrameworkStores<TestDbContext>();

    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
