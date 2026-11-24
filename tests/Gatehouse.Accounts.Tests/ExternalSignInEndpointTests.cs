using System.Net;
using System.Net.Http.Json;
using System.Web;

namespace Gatehouse.Accounts.Tests;

public sealed class ExternalSignInEndpointTests
{
    // The test server's client never follows redirects, so the tests can look at them.
    private static Task<HttpResponseMessage> GetAsync(TestApp app, string url) => app.Client.GetAsync(url);

    [Fact]
    public async Task Providers_lists_the_switched_on_ones()
    {
        await using var app = await TestApp.StartAsync();

        var names = await app.Client.GetFromJsonAsync<string[]>("/api/auth/providers");

        Assert.Equal(["Google"], Assert.IsType<string[]>(names));
    }

    [Fact]
    public async Task An_unknown_provider_goes_back_to_the_login_page()
    {
        await using var app = await TestApp.StartAsync();

        var response = await GetAsync(app, "/api/auth/external/nope?returnTo=/x");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login?error=external-failed&returnTo=%2Fx", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task A_known_provider_sends_the_browser_to_the_provider()
    {
        await using var app = await TestApp.StartAsync();

        var response = await GetAsync(app, "/api/auth/external/google?returnTo=/x");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.Equal("accounts.google.com", location.Host);
        var query = HttpUtility.ParseQueryString(location.Query);
        Assert.EndsWith("/api/auth/signin-google", query["redirect_uri"]);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"),
            c => c.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task The_callback_without_a_provider_answer_fails_and_cleans_the_return_address()
    {
        await using var app = await TestApp.StartAsync();

        var response = await GetAsync(app, "/api/auth/external/callback?returnTo=//evil.com");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login?error=external-failed&returnTo=%2F", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task A_failed_provider_return_goes_back_to_the_login_page()
    {
        await using var app = await TestApp.StartAsync();

        var response = await GetAsync(app, "/api/auth/signin-google?state=bad");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login?error=external-failed", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task A_custom_login_path_is_used()
    {
        await using var app = await TestApp.StartAsync(o => o.LoginPath = "/sign-in");

        var unknown = await GetAsync(app, "/api/auth/external/nope");
        var failed = await GetAsync(app, "/api/auth/signin-google?state=bad");

        Assert.Equal("/sign-in?error=external-failed&returnTo=%2F", unknown.Headers.Location?.OriginalString);
        Assert.Equal("/sign-in?error=external-failed", failed.Headers.Location?.OriginalString);
    }
}
