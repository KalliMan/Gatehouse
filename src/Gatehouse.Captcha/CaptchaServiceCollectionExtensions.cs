using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gatehouse.Captcha;

/// <summary>Registers the captcha services.</summary>
public static class CaptchaServiceCollectionExtensions
{
    /// <summary>
    /// Registers the verifier for Captcha:Provider. A misconfigured captcha throws an
    /// <see cref="InvalidOperationException"/>, so the app stops at startup.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The app's configuration; the "Captcha" section is read.</param>
    /// <param name="configureEndpoints">Changes the header name or the failure answer.</param>
    public static IServiceCollection AddGatehouseCaptcha(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<CaptchaEndpointOptions>? configureEndpoints = null)
    {
        var section = configuration.GetSection(CaptchaOptions.SectionName);
        services.AddOptions<CaptchaOptions>().Bind(section);
        var options = section.Get<CaptchaOptions>() ?? new();

        var endpointOptions = new CaptchaEndpointOptions();
        configureEndpoints?.Invoke(endpointOptions);
        services.AddSingleton(endpointOptions);

        services.TryAddSingleton(TimeProvider.System);

        switch (options.Provider)
        {
            case CaptchaProvider.Turnstile:
                if (string.IsNullOrWhiteSpace(options.Turnstile.SiteKey)
                    || string.IsNullOrWhiteSpace(options.Turnstile.SecretKey))
                {
                    throw new InvalidOperationException(
                        "Captcha:Provider is Turnstile, but Captcha:Turnstile:SiteKey or Captcha:Turnstile:SecretKey is not set.");
                }

                services.AddHttpClient<ICaptchaVerifier, TurnstileVerifier>(http =>
                    http.Timeout = TimeSpan.FromSeconds(10));
                break;

            case CaptchaProvider.Altcha:
                var altcha = options.Altcha;
                Require(altcha.Cost >= 1, "Captcha:Altcha:Cost must be at least 1.");
                Require(altcha.MinCounter >= 0, "Captcha:Altcha:MinCounter must be at least 0.");
                Require(altcha.MaxCounter >= altcha.MinCounter,
                    "Captcha:Altcha:MaxCounter must be at least Captcha:Altcha:MinCounter.");
                Require(altcha.MaxCounter < int.MaxValue, "Captcha:Altcha:MaxCounter must be less than 2147483647.");
                Require(altcha.LifetimeMinutes >= 1, "Captcha:Altcha:LifetimeMinutes must be at least 1.");

                // One instance: it holds the signing key and the used puzzles.
                services.AddSingleton<AltchaCaptcha>();
                services.AddSingleton<ICaptchaVerifier>(sp => sp.GetRequiredService<AltchaCaptcha>());
                break;

            default:
                services.AddSingleton<ICaptchaVerifier, NoCaptcha>();
                break;
        }

        return services;
    }

    private static void Require(bool valid, string message)
    {
        if (!valid)
        {
            throw new InvalidOperationException(message);
        }
    }
}
