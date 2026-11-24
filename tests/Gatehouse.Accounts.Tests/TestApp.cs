using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gatehouse.Accounts.Tests;

/// <summary>A small app on the test server: Identity on an in-memory database, Google with fake keys and the package endpoints.</summary>
internal sealed class TestApp : IAsyncDisposable
{
    private readonly WebApplication _app;

    private TestApp(WebApplication app) => _app = app;

    /// <summary>A client that does not follow redirects, so a test can look at them.</summary>
    public HttpClient Client => _app.GetTestClient();

    public static async Task<TestApp> StartAsync(Action<ExternalSignInOptions>? configure = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Google:ClientId"] = "google-id",
            ["Authentication:Google:ClientSecret"] = "google-secret",
        });

        var databaseName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase(databaseName));
        builder.Services.AddIdentity<TestUser, IdentityRole<Guid>>()
            .AddEntityFrameworkStores<TestDbContext>()
            .AddDefaultTokenProviders();
        builder.Services.AddSingleton<TestHooksState>();
        builder.Services.AddGatehouseExternalSignIn<TestUser, TestHooks>(builder.Configuration, configure);

        var app = builder.Build();
        app.UseAuthentication();
        app.MapExternalProviders();
        app.MapExternalSignIn<TestUser>();
        await app.StartAsync();
        return new TestApp(app);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
