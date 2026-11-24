using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gatehouse.Accounts.Tests;

/// <summary>Identity's own endpoints, with the package's sender in place of Identity's default one.</summary>
public sealed class AccountEmailIdentityTests
{
    private const string Email = "ann@example.com";
    private const string Password = "Passw0rd!x";

    private sealed class Host(WebApplication app, CapturedEmails captured) : IAsyncDisposable
    {
        public HttpClient Client { get; } = app.GetTestClient();

        public CapturedEmails Captured { get; } = captured;

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private static async Task<Host> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Delivery"] = "Log",
            ["Email:FrontendBaseUrl"] = "https://app.example",
        });

        var databaseName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase(databaseName));
        builder.Services.AddIdentityApiEndpoints<TestUser>(o => o.SignIn.RequireConfirmedEmail = true)
            .AddEntityFrameworkStores<TestDbContext>();
        builder.Services.AddSingleton<ComposerState>();
        builder.Services.AddGatehouseAccountEmails<TestUser, TestComposer>(builder.Configuration);
        var captured = new CapturedEmails();
        builder.Services.AddSingleton<IEmailDelivery>(captured);

        var app = builder.Build();
        app.MapGroup("/api/auth").MapIdentityApi<TestUser>();
        await app.StartAsync();
        return new Host(app, captured);
    }

    private static async Task<(string UserId, string Code)> RegisterAsync(Host host)
    {
        var response = await host.Client.PostAsJsonAsync("/api/auth/register", new { email = Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var sent = Assert.Single(host.Captured.Sent);
        Assert.Equal("ConfirmEmail", sent.Subject);
        Assert.StartsWith("https://app.example/confirm-email?userId=", sent.TextBody);
        var query = QueryHelpers.ParseQuery(new Uri(sent.TextBody).Query);
        return (query["userId"].ToString(), query["code"].ToString());
    }

    [Fact]
    public async Task Registering_sends_a_confirmation_link_that_Identity_accepts()
    {
        await using var host = await StartAsync();
        var (userId, code) = await RegisterAsync(host);

        var confirm = await host.Client.GetAsync(
            $"/api/auth/confirmEmail?userId={Uri.EscapeDataString(userId)}&code={Uri.EscapeDataString(code)}");

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
    }

    [Fact]
    public async Task A_forgotten_password_can_be_reset_with_the_emailed_link()
    {
        await using var host = await StartAsync();
        var (userId, code) = await RegisterAsync(host);
        var confirm = await host.Client.GetAsync(
            $"/api/auth/confirmEmail?userId={Uri.EscapeDataString(userId)}&code={Uri.EscapeDataString(code)}");
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        host.Captured.Sent.Clear();

        var forgot = await host.Client.PostAsJsonAsync("/api/auth/forgotPassword", new { email = Email });

        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        var sent = Assert.Single(host.Captured.Sent);
        Assert.Equal("ResetPassword", sent.Subject);
        Assert.StartsWith("https://app.example/reset-password?email=", sent.TextBody);
        var query = QueryHelpers.ParseQuery(new Uri(sent.TextBody).Query);
        Assert.Equal(Email, query["email"].ToString());

        var reset = await host.Client.PostAsJsonAsync(
            "/api/auth/resetPassword",
            new { email = query["email"].ToString(), resetCode = query["code"].ToString(), newPassword = "N3w-Passw0rd!" });

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
    }
}
