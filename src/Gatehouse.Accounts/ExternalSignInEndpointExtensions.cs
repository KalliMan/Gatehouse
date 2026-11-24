using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Gatehouse.Accounts;

/// <summary>Maps the endpoints of provider sign-in. They have no rate limit or captcha of their own: add them to the returned builders.</summary>
public static class ExternalSignInEndpointExtensions
{
    /// <summary>Maps a GET endpoint that returns the names of the switched-on providers, for example <c>["Google"]</c>.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <param name="pattern">The route. Default "/api/auth/providers".</param>
    /// <returns>The endpoint builder.</returns>
    public static RouteHandlerBuilder MapExternalProviders(
        this IEndpointRouteBuilder endpoints, string pattern = "/api/auth/providers") =>
        endpoints.MapGet(pattern, (ExternalProviderList providers) => TypedResults.Ok(providers.Names));

    /// <summary>
    /// Maps the two browser redirects of provider sign-in under <paramref name="prefix"/>:
    /// <c>GET {prefix}/{provider}?returnTo=</c> sends the browser to the provider, and
    /// <c>GET {prefix}/callback?returnTo=</c> finishes the sign-in. They are left out of the OpenAPI document.
    /// Errors go to the sign-in page as <c>?error=...</c> (see the package README for the codes).
    /// </summary>
    /// <typeparam name="TUser">The Identity user class.</typeparam>
    /// <param name="endpoints">The route builder.</param>
    /// <param name="prefix">The route prefix. Default "/api/auth/external".</param>
    /// <returns>The route group, to add a rate limit, a captcha or other conventions to both endpoints.</returns>
    public static RouteGroupBuilder MapExternalSignIn<TUser>(
        this IEndpointRouteBuilder endpoints, string prefix = "/api/auth/external")
        where TUser : class
    {
        var group = endpoints.MapGroup(prefix);
        var callbackPath = prefix.TrimEnd('/') + "/callback";

        group.MapGet("/{provider}", (
                string provider,
                string? returnTo,
                ExternalProviderList providers,
                ExternalSignIn<TUser> signIn,
                IOptions<ExternalSignInOptions> options) =>
            {
                var target = ReturnUrl.Sanitize(returnTo);
                if (providers.Find(provider) is not { } scheme)
                {
                    return Results.Redirect(LoginUrl(options.Value, "external-failed", target));
                }

                var callback = $"{callbackPath}?returnTo={Uri.EscapeDataString(target)}";
                return Results.Challenge(signIn.CreateChallenge(scheme, callback), [scheme]);
            })
            .ExcludeFromDescription();

        group.MapGet("/callback", async (
                string? returnTo,
                HttpContext http,
                ExternalSignIn<TUser> signIn,
                IOptions<ExternalSignInOptions> options) =>
            {
                var result = await signIn.CompleteAsync(http.RequestAborted);
                var target = ReturnUrl.Sanitize(returnTo);
                return Results.Redirect(result == ExternalSignInResult.Success
                    ? target
                    : LoginUrl(options.Value, ErrorCode(result), target));
            })
            .ExcludeFromDescription();

        return group;
    }

    private static string LoginUrl(ExternalSignInOptions options, string error, string target) =>
        $"{options.LoginPath}?error={error}&returnTo={Uri.EscapeDataString(target)}";

    private static string ErrorCode(ExternalSignInResult result) => result switch
    {
        ExternalSignInResult.NoEmail => "external-no-email",
        ExternalSignInResult.EmailInUse => "external-email-in-use",
        ExternalSignInResult.LockedOut => "external-locked-out",
        ExternalSignInResult.Disabled => "external-disabled",
        ExternalSignInResult.SignUpClosed => "external-sign-up-closed",
        _ => "external-failed",
    };
}
