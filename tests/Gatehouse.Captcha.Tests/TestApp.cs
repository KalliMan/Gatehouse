using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gatehouse.Captcha.Tests;

/// <summary>
/// A small app on the test server: the captcha services, the two captcha endpoints and two protected endpoints
/// (/protected, and /anonymous-only where a request with the header X-Test-User counts as signed in).
/// </summary>
internal sealed class TestApp : IAsyncDisposable
{
    private readonly WebApplication _app;

    private TestApp(WebApplication app) => _app = app;

    public HttpClient Client => _app.GetTestClient();

    public IServiceProvider Services => _app.Services;

    public static async Task<TestApp> StartAsync(
        Dictionary<string, string?> config,
        Action<CaptchaEndpointOptions>? configureEndpoints = null,
        TimeProvider? time = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(config);
        if (time is not null)
        {
            builder.Services.AddSingleton(time);
        }

        builder.Services.AddGatehouseCaptcha(builder.Configuration, configureEndpoints);

        var app = builder.Build();
        app.Use(async (http, next) =>
        {
            if (http.Request.Headers.ContainsKey("X-Test-User"))
            {
                http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "tester")], "Test"));
            }

            await next();
        });
        app.MapCaptchaSettings();
        app.MapCaptchaChallenge();
        app.MapPost("/protected", () => Results.Ok("ok")).RequireCaptcha();
        app.MapPost("/anonymous-only", () => Results.Ok("ok")).RequireCaptcha(anonymousOnly: true);
        await app.StartAsync();
        return new TestApp(app);
    }

    public static Dictionary<string, string?> AltchaConfig() => new()
    {
        ["Captcha:Provider"] = "Altcha",
        ["Captcha:Altcha:Cost"] = "1",
        ["Captcha:Altcha:MinCounter"] = "1",
        ["Captcha:Altcha:MaxCounter"] = "20",
    };

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
