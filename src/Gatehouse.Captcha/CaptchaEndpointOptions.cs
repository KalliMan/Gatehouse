using Microsoft.AspNetCore.Http;

namespace Gatehouse.Captcha;

/// <summary>How the endpoint filter reads the token and what it answers when the check fails.</summary>
public sealed class CaptchaEndpointOptions
{
    /// <summary>The request header that carries the token. Default "Captcha-Token".</summary>
    public string TokenHeader { get; set; } = "Captcha-Token";

    /// <summary>
    /// Makes the answer for a request that didn't pass. Default: a ProblemDetails 400 with the extension
    /// <c>code</c> set to <c>Captcha.Required</c> or <c>Captcha.Failed</c>.
    /// </summary>
    public Func<HttpContext, CaptchaFailure, IResult> OnFailure { get; set; } = DefaultOnFailure;

    private static IResult DefaultOnFailure(HttpContext http, CaptchaFailure failure) =>
        failure == CaptchaFailure.Missing
            ? Problem("Captcha.Required", "Please confirm you're not a robot.")
            : Problem("Captcha.Failed", "The check didn't pass. Please try it again.");

    private static IResult Problem(string code, string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Bad request",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
