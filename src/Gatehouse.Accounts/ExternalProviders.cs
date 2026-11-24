using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Gatehouse.Accounts;

/// <summary>
/// The provider names. They are also the authentication scheme names and the names returned by
/// <see cref="ExternalProviderList.Names"/>.
/// </summary>
public static class ExternalProviders
{
    /// <summary>Google.</summary>
    public const string Google = "Google";

    /// <summary>Microsoft (personal, work and school accounts).</summary>
    public const string Microsoft = "Microsoft";

    /// <summary>Facebook.</summary>
    public const string Facebook = "Facebook";

    /// <summary>GitHub.</summary>
    public const string GitHub = "GitHub";
}

/// <summary>
/// Switches the providers on from configuration: "Authentication:{Provider}:ClientId" and "ClientSecret" must both be set.
/// </summary>
internal static class ExternalProviderSetup
{
    /// <summary>The claim set from Google's "email_verified" flag; only such emails may be linked to an existing account.</summary>
    public const string EmailVerifiedClaim = "email_verified";

    private sealed record Credentials(string ClientId, string ClientSecret);

    /// <summary>Adds every provider that has credentials and returns their names in ordinal order.</summary>
    public static IReadOnlyList<string> Add(
        AuthenticationBuilder auth, IConfiguration configuration, ExternalSignInOptions options)
    {
        var names = new List<string>();

        if (Find(configuration, ExternalProviders.Google) is { } google)
        {
            auth.AddGoogle(o =>
            {
                Configure(o, google, ExternalProviders.Google, options);
                // The OpenID form is "email_verified"; the handler's userinfo endpoint (v2) calls it "verified_email".
                o.ClaimActions.MapJsonKey(EmailVerifiedClaim, "email_verified");
                o.ClaimActions.MapJsonKey(EmailVerifiedClaim, "verified_email");
            });
            names.Add(ExternalProviders.Google);
        }

        if (Find(configuration, ExternalProviders.Microsoft) is { } microsoft)
        {
            auth.AddMicrosoftAccount(o => Configure(o, microsoft, ExternalProviders.Microsoft, options));
            names.Add(ExternalProviders.Microsoft);
        }

        if (Find(configuration, ExternalProviders.Facebook) is { } facebook)
        {
            auth.AddFacebook(o => Configure(o, facebook, ExternalProviders.Facebook, options));
            names.Add(ExternalProviders.Facebook);
        }

        if (Find(configuration, ExternalProviders.GitHub) is { } github)
        {
            auth.AddGitHub(o =>
            {
                Configure(o, github, ExternalProviders.GitHub, options);
                // Without it, GitHub sends an email only when the user made one public.
                o.Scope.Add("user:email");
            });
            names.Add(ExternalProviders.GitHub);
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>Whether the provider vouches that this person owns the email address.</summary>
    public static bool IsEmailVerified(ExternalLoginInfo info) => info.LoginProvider switch
    {
        ExternalProviders.Google =>
            bool.TryParse(info.Principal.FindFirstValue(EmailVerifiedClaim), out var verified) && verified,
        // GitHub's public email must be verified, and the user:email scope returns only the primary verified address.
        ExternalProviders.GitHub => true,
        // Microsoft work accounts can carry an email address their organization never verified; Facebook does not say.
        _ => false,
    };

    private static Credentials? Find(IConfiguration configuration, string provider)
    {
        var section = configuration.GetSection($"Authentication:{provider}");
        var (id, secret) = (section["ClientId"], section["ClientSecret"]);
        return string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret) ? null : new(id, secret);
    }

    private static void Configure(OAuthOptions options, Credentials credentials, string name, ExternalSignInOptions signIn)
    {
        options.ClientId = credentials.ClientId;
        options.ClientSecret = credentials.ClientSecret;
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.CallbackPath = signIn.CallbackPathPrefix + name.ToLowerInvariant();

        // The provider returns with a top-level GET, which carries Lax cookies. The default (None) needs HTTPS, so
        // the browser would drop it on http://localhost and the sign-in would fail with "Correlation failed".
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;

        // Cancelled at the provider or anything else gone wrong: back to the sign-in page with a message, not a 500.
        options.Events.OnAccessDenied = context => RedirectToLogin(context, "external-cancelled");
        options.Events.OnRemoteFailure = context => RedirectToLogin(context, "external-failed");
    }

    private static Task RedirectToLogin<TOptions>(HandleRequestContext<TOptions> context, string error)
        where TOptions : RemoteAuthenticationOptions
    {
        var loginPath = context.HttpContext.RequestServices.GetRequiredService<IOptions<ExternalSignInOptions>>().Value.LoginPath;
        context.Response.Redirect($"{loginPath}?error={error}");
        context.HandleResponse();
        return Task.CompletedTask;
    }
}
