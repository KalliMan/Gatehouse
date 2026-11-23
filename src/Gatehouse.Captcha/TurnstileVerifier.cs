using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gatehouse.Captcha;

/// <summary>
/// Cloudflare Turnstile: the widget gives the browser a token, and Cloudflare's siteverify says whether it is valid.
/// Cloudflare also makes each token single-use and lets it expire after 5 minutes.
/// </summary>
internal sealed partial class TurnstileVerifier(
    HttpClient http,
    IOptions<CaptchaOptions> options,
    ILogger<TurnstileVerifier> logger) : ICaptchaVerifier
{
    private const string SiteVerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    // Cloudflare's documented maximum; anything longer isn't a token and isn't worth a request.
    private const int MaxTokenLength = 2048;

    private sealed record SiteVerifyResponse(
        bool Success,
        [property: JsonPropertyName("error-codes")] string[]? ErrorCodes);

    public bool Enabled => true;

    public async Task<bool> VerifyAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength)
        {
            return false;
        }

        // The visitor's IP address (remoteip) is optional and left out: Cloudflare doesn't need it to check the token.
        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("secret", options.Value.Turnstile.SecretKey),
            new KeyValuePair<string, string>("response", token),
        ]);
        try
        {
            using var response = await http.PostAsync(SiteVerifyUrl, content, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<SiteVerifyResponse>(ct);
            if (result?.Success == true)
            {
                return true;
            }

            LogRefused(logger, string.Join(", ", result?.ErrorCodes ?? []));
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // Fail closed: if Cloudflare can't be asked, the captcha doesn't pass. Letting it pass would switch the
            // protection off for anyone who can make the check fail.
            LogUnreachable(logger, ex);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Turnstile refused a token: {ErrorCodes}")]
    private static partial void LogRefused(ILogger logger, string errorCodes);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Turnstile could not be reached, so the captcha did not pass")]
    private static partial void LogUnreachable(ILogger logger, Exception exception);
}
