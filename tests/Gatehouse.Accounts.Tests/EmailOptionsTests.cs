using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Gatehouse.Accounts.Tests;

public sealed class EmailOptionsTests
{
    private static async Task<WebApplication> StartAsync(string environment, Dictionary<string, string?> config)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(config);
        builder.Services.AddSingleton<ComposerState>();
        builder.Services.AddGatehouseAccountEmails<TestUser, TestComposer>(builder.Configuration);

        var app = builder.Build();
        try
        {
            await app.StartAsync();
            return app;
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    private static Dictionary<string, string?> Smtp() => new()
    {
        ["Email:Delivery"] = "Smtp",
        ["Email:FrontendBaseUrl"] = "https://app.example",
        ["Email:FromAddress"] = "noreply@app.example",
        ["Email:Smtp:Host"] = "smtp.example",
    };

    [Fact]
    public async Task Log_delivery_in_production_stops_the_start()
    {
        var config = new Dictionary<string, string?>
        {
            ["Email:Delivery"] = "Log",
            ["Email:FrontendBaseUrl"] = "https://app.example",
        };

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(() => StartAsync("Production", config));

        Assert.Contains("Development only", ex.Message);
    }

    [Fact]
    public async Task Smtp_without_a_host_stops_the_start()
    {
        var config = Smtp();
        config.Remove("Email:Smtp:Host");

        await Assert.ThrowsAsync<OptionsValidationException>(() => StartAsync("Production", config));
    }

    [Fact]
    public async Task Smtp_with_a_port_out_of_range_stops_the_start()
    {
        var config = Smtp();
        config["Email:Smtp:Port"] = "70000";

        await Assert.ThrowsAsync<OptionsValidationException>(() => StartAsync("Production", config));
    }

    [Theory]
    [InlineData("/confirm")]
    [InlineData("")]
    [InlineData("ftp://app.example")]
    public async Task A_frontend_address_that_is_not_an_absolute_http_url_stops_the_start(string baseUrl)
    {
        var config = Smtp();
        config["Email:FrontendBaseUrl"] = baseUrl;

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(() => StartAsync("Production", config));

        Assert.Contains("FrontendBaseUrl", ex.Message);
    }

    [Fact]
    public async Task None_with_nothing_set_starts_and_sends_nothing()
    {
        await using var app = await StartAsync("Production", []);

        var delivery = app.Services.GetRequiredService<IEmailDelivery>();

        Assert.IsType<NoEmailDelivery>(delivery);
        Assert.False(app.Services.GetRequiredService<IOptions<EmailOptions>>().Value.Enabled);
        await delivery.SendAsync(new EmailMessage("a@example.com", "s", "<p>h</p>", "t"), default);
    }

    [Fact]
    public async Task Log_delivery_in_development_is_the_log_delivery()
    {
        var config = new Dictionary<string, string?>
        {
            ["Email:Delivery"] = "Log",
            ["Email:FrontendBaseUrl"] = "https://app.example",
        };
        await using var app = await StartAsync("Development", config);

        Assert.IsType<LogEmailDelivery>(app.Services.GetRequiredService<IEmailDelivery>());
        Assert.True(app.Services.GetRequiredService<IOptions<EmailOptions>>().Value.Enabled);
    }

    [Fact]
    public async Task Full_smtp_settings_give_the_smtp_delivery()
    {
        await using var app = await StartAsync("Production", Smtp());

        Assert.IsType<SmtpEmailDelivery>(app.Services.GetRequiredService<IEmailDelivery>());
    }

    [Fact]
    public async Task The_paths_have_defaults()
    {
        await using var app = await StartAsync("Production", []);

        var options = app.Services.GetRequiredService<IOptions<EmailOptions>>().Value;

        Assert.Equal("confirm-email", options.ConfirmEmailPath);
        Assert.Equal("reset-password", options.ResetPasswordPath);
        Assert.Equal(587, options.Smtp.Port);
    }
}
