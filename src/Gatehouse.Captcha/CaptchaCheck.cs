using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Gatehouse.Captcha;

/// <summary>The captcha check itself, for code that isn't an endpoint filter.</summary>
public static class CaptchaCheck
{
    /// <summary>Null = passed (or no captcha is configured); otherwise the failure answer to return.</summary>
    /// <param name="http">The current request.</param>
    public static async Task<IResult?> RunAsync(HttpContext http)
    {
        var verifier = http.RequestServices.GetRequiredService<ICaptchaVerifier>();
        if (!verifier.Enabled)
        {
            return null;
        }

        var endpointOptions = http.RequestServices.GetRequiredService<CaptchaEndpointOptions>();
        string? token = http.Request.Headers[endpointOptions.TokenHeader];
        if (string.IsNullOrEmpty(token))
        {
            return endpointOptions.OnFailure(http, CaptchaFailure.Missing);
        }

        return await verifier.VerifyAsync(token, http.RequestAborted)
            ? null
            : endpointOptions.OnFailure(http, CaptchaFailure.Invalid);
    }
}
