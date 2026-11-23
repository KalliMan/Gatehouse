using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Gatehouse.Captcha.Tests;

public class TurnstileVerifierTests
{
    private static TurnstileVerifier Create(FakeHttpHandler handler) =>
        new(
            new HttpClient(handler),
            Options.Create(new CaptchaOptions
            {
                Provider = CaptchaProvider.Turnstile,
                Turnstile = new TurnstileOptions { SiteKey = "site", SecretKey = "the-secret" },
            }),
            NullLogger<TurnstileVerifier>.Instance);

    [Fact]
    public async Task Success_true_passes()
    {
        var handler = FakeHttpHandler.Json("""{"success":true}""");

        Assert.True(await Create(handler).VerifyAsync("token"));
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task The_secret_and_the_token_are_sent_to_siteverify()
    {
        var handler = FakeHttpHandler.Json("""{"success":true}""");

        await Create(handler).VerifyAsync("the-token");

        Assert.Equal("https://challenges.cloudflare.com/turnstile/v0/siteverify", handler.LastUri?.ToString());
        Assert.Contains("secret=the-secret", handler.LastBody);
        Assert.Contains("response=the-token", handler.LastBody);
        Assert.DoesNotContain("remoteip", handler.LastBody);
    }

    [Fact]
    public async Task Success_false_is_refused()
    {
        var handler = FakeHttpHandler.Json("""{"success":false,"error-codes":["invalid-input-response"]}""");

        Assert.False(await Create(handler).VerifyAsync("token"));
    }

    [Fact]
    public async Task An_http_error_is_refused()
    {
        var handler = FakeHttpHandler.Json("""{"success":true}""", HttpStatusCode.InternalServerError);

        Assert.False(await Create(handler).VerifyAsync("token"));
    }

    [Fact]
    public async Task A_thrown_http_request_exception_is_refused()
    {
        var handler = new FakeHttpHandler(_ => throw new HttpRequestException("no network"));

        Assert.False(await Create(handler).VerifyAsync("token"));
    }

    [Fact]
    public async Task Bad_json_is_refused()
    {
        var handler = FakeHttpHandler.Json("this is not json");

        Assert.False(await Create(handler).VerifyAsync("token"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task An_empty_token_sends_no_request(string? token)
    {
        var handler = FakeHttpHandler.Json("""{"success":true}""");

        Assert.False(await Create(handler).VerifyAsync(token));
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task A_token_over_2048_characters_sends_no_request()
    {
        var handler = FakeHttpHandler.Json("""{"success":true}""");

        Assert.False(await Create(handler).VerifyAsync(new string('a', 2049)));
        Assert.Equal(0, handler.RequestCount);
    }
}
