using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

namespace Gatehouse.Captcha.Tests;

public class CaptchaEndpointTests
{
    private static async Task<string> SolvedTokenAsync(HttpClient client, string pattern = "/api/captcha/challenge")
    {
        var challenge = await client.GetFromJsonAsync<AltchaChallenge>(pattern);
        return AltchaSolver.SolveToToken(challenge!);
    }

    private static HttpRequestMessage Post(string path, string? header = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (header is not null && token is not null)
        {
            request.Headers.Add(header, token);
        }

        return request;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task No_header_gives_400_Captcha_Required()
    {
        await using var app = await TestApp.StartAsync(TestApp.AltchaConfig());

        var response = await app.Client.SendAsync(Post("/protected"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("Captcha.Required", body.GetProperty("code").GetString());
        Assert.Equal("Bad request", body.GetProperty("title").GetString());
        Assert.Equal("Please confirm you're not a robot.", body.GetProperty("detail").GetString());
        Assert.Equal(400, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task A_bad_token_gives_400_Captcha_Failed()
    {
        await using var app = await TestApp.StartAsync(TestApp.AltchaConfig());

        var response = await app.Client.SendAsync(Post("/protected", "Captcha-Token", "garbage"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("Captcha.Failed", body.GetProperty("code").GetString());
        Assert.Equal("The check didn't pass. Please try it again.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_solved_token_gives_200_and_works_once()
    {
        await using var app = await TestApp.StartAsync(TestApp.AltchaConfig());
        var token = await SolvedTokenAsync(app.Client);

        var first = await app.Client.SendAsync(Post("/protected", "Captcha-Token", token));
        var second = await app.Client.SendAsync(Post("/protected", "Captcha-Token", token));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Provider_None_gives_200_without_a_header()
    {
        await using var app = await TestApp.StartAsync([]);

        var response = await app.Client.SendAsync(Post("/protected"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_custom_failure_answer_is_used()
    {
        await using var app = await TestApp.StartAsync(
            TestApp.AltchaConfig(),
            o => o.OnFailure = (_, failure) => failure == CaptchaFailure.Missing
                ? Results.StatusCode(418)
                : Results.StatusCode(429));

        var missing = await app.Client.SendAsync(Post("/protected"));
        var invalid = await app.Client.SendAsync(Post("/protected", "Captcha-Token", "garbage"));

        Assert.Equal((HttpStatusCode)418, missing.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, invalid.StatusCode);
    }

    [Fact]
    public async Task A_custom_token_header_is_used()
    {
        await using var app = await TestApp.StartAsync(TestApp.AltchaConfig(), o => o.TokenHeader = "X-Captcha");
        var token = await SolvedTokenAsync(app.Client);

        var withDefaultHeader = await app.Client.SendAsync(Post("/protected", "Captcha-Token", token));
        var withCustomHeader = await app.Client.SendAsync(Post("/protected", "X-Captcha", token));

        Assert.Equal(HttpStatusCode.BadRequest, withDefaultHeader.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withCustomHeader.StatusCode);
    }

    [Fact]
    public async Task AnonymousOnly_asks_anonymous_callers_but_not_signed_in_ones()
    {
        await using var app = await TestApp.StartAsync(TestApp.AltchaConfig());

        var anonymous = await app.Client.SendAsync(Post("/anonymous-only"));
        var signedIn = await app.Client.SendAsync(Post("/anonymous-only", "X-Test-User", "yes"));

        Assert.Equal(HttpStatusCode.BadRequest, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
    }

    [Fact]
    public async Task The_challenge_endpoint_gives_404_for_None()
    {
        await using var app = await TestApp.StartAsync([]);

        var response = await app.Client.GetAsync("/api/captcha/challenge");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_challenge_endpoint_gives_404_for_Turnstile()
    {
        await using var app = await TestApp.StartAsync(new()
        {
            ["Captcha:Provider"] = "Turnstile",
            ["Captcha:Turnstile:SiteKey"] = "site",
            ["Captcha:Turnstile:SecretKey"] = "secret",
        });

        var response = await app.Client.GetAsync("/api/captcha/challenge");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_challenge_endpoint_gives_a_no_store_puzzle_for_Altcha()
    {
        await using var app = await TestApp.StartAsync(TestApp.AltchaConfig());

        var response = await app.Client.GetAsync("/api/captcha/challenge");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await ReadJsonAsync(response);
        Assert.Equal("PBKDF2/SHA-256", body.GetProperty("parameters").GetProperty("algorithm").GetString());
        Assert.Equal(1, body.GetProperty("parameters").GetProperty("cost").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("signature").GetString()));
    }

    [Fact]
    public async Task The_settings_endpoint_shows_the_site_key_only_for_Turnstile()
    {
        await using var turnstile = await TestApp.StartAsync(new()
        {
            ["Captcha:Provider"] = "Turnstile",
            ["Captcha:Turnstile:SiteKey"] = "the-site-key",
            ["Captcha:Turnstile:SecretKey"] = "secret",
        });
        await using var altcha = await TestApp.StartAsync(TestApp.AltchaConfig());
        await using var none = await TestApp.StartAsync([]);

        var forTurnstile = await ReadJsonAsync(await turnstile.Client.GetAsync("/api/captcha"));
        var forAltcha = await ReadJsonAsync(await altcha.Client.GetAsync("/api/captcha"));
        var forNone = await ReadJsonAsync(await none.Client.GetAsync("/api/captcha"));

        Assert.Equal("Turnstile", forTurnstile.GetProperty("provider").GetString());
        Assert.Equal("the-site-key", forTurnstile.GetProperty("turnstileSiteKey").GetString());
        Assert.Equal("Altcha", forAltcha.GetProperty("provider").GetString());
        Assert.Equal(JsonValueKind.Null, forAltcha.GetProperty("turnstileSiteKey").ValueKind);
        Assert.Equal("None", forNone.GetProperty("provider").GetString());
        Assert.Equal(JsonValueKind.Null, forNone.GetProperty("turnstileSiteKey").ValueKind);
    }

    [Fact]
    public async Task The_endpoints_can_be_mapped_on_other_routes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(TestApp.AltchaConfig());
        builder.Services.AddGatehouseCaptcha(builder.Configuration);
        await using var app = builder.Build();
        app.MapCaptchaSettings("/x/settings");
        app.MapCaptchaChallenge("/x/challenge");
        await app.StartAsync();
        var client = app.GetTestClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/x/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/x/challenge")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/captcha")).StatusCode);
    }
}
