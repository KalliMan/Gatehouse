using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Gatehouse.Captcha;

/// <summary>Which captcha the forms use.</summary>
/// <param name="Provider">"None", "Altcha" or "Turnstile".</param>
/// <param name="TurnstileSiteKey">Only with Turnstile (public), else null.</param>
public sealed record CaptchaSettings(string Provider, string? TurnstileSiteKey);

/// <summary>Adds the captcha check and the captcha endpoints to minimal APIs.</summary>
public static class CaptchaEndpointExtensions
{
    /// <summary>The endpoint needs a solved captcha in the token header (Captcha-Token by default).</summary>
    /// <param name="builder">The endpoint or group.</param>
    /// <param name="anonymousOnly">True: signed-in callers aren't asked.</param>
    public static TBuilder RequireCaptcha<TBuilder>(this TBuilder builder, bool anonymousOnly = false)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            if (anonymousOnly && context.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                return await next(context);
            }

            return await CaptchaCheck.RunAsync(context.HttpContext) ?? await next(context);
        });
        return builder;
    }

    /// <summary>Maps a GET endpoint that tells the page which captcha to show.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <param name="pattern">The route. Default "/api/captcha".</param>
    public static RouteHandlerBuilder MapCaptchaSettings(
        this IEndpointRouteBuilder endpoints, string pattern = "/api/captcha") =>
        endpoints.MapGet(pattern, (IOptions<CaptchaOptions> options) =>
        {
            var captcha = options.Value;
            return Results.Ok(new CaptchaSettings(
                captcha.Provider.ToString(),
                captcha.Provider == CaptchaProvider.Turnstile ? captcha.Turnstile.SiteKey : null));
        });

    /// <summary>
    /// Maps a GET endpoint that returns a new ALTCHA puzzle for the widget (404 unless the provider is Altcha).
    /// It has no rate limit of its own: add one to the returned builder.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <param name="pattern">The route. Default "/api/captcha/challenge".</param>
    public static RouteHandlerBuilder MapCaptchaChallenge(
        this IEndpointRouteBuilder endpoints, string pattern = "/api/captcha/challenge") =>
        endpoints.MapGet(pattern, (HttpContext http) =>
        {
            // Only with Captcha:Provider Altcha is the verifier registered.
            if (http.RequestServices.GetService<AltchaCaptcha>() is not { } altcha)
            {
                return Results.NotFound();
            }

            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(altcha.CreateChallenge());
        });
}
