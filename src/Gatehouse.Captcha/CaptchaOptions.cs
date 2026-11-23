namespace Gatehouse.Captcha;

/// <summary>Which captcha an app uses.</summary>
public enum CaptchaProvider
{
    /// <summary>No captcha: nothing asks for a token.</summary>
    None,

    /// <summary>Self-hosted proof of work: the browser computes for a second or two, nothing leaves the server.</summary>
    Altcha,

    /// <summary>Cloudflare's hosted captcha: needs the two keys and sends the visitor's browser to Cloudflare.</summary>
    Turnstile,
}

/// <summary>The "Captcha" configuration section.</summary>
public sealed class CaptchaOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Captcha";

    /// <summary>Which captcha is used. Default: <see cref="CaptchaProvider.None"/>.</summary>
    public CaptchaProvider Provider { get; init; } = CaptchaProvider.None;

    /// <summary>Settings for <see cref="CaptchaProvider.Altcha"/>.</summary>
    public AltchaOptions Altcha { get; init; } = new();

    /// <summary>Settings for <see cref="CaptchaProvider.Turnstile"/>.</summary>
    public TurnstileOptions Turnstile { get; init; } = new();
}

/// <summary>The "Captcha:Altcha" section. The defaults are altcha-lib's recommended values.</summary>
public sealed class AltchaOptions
{
    /// <summary>PBKDF2 iterations per attempt. Default 5000.</summary>
    public int Cost { get; init; } = 5_000;

    /// <summary>Lowest counter the server picks for a puzzle. Default 5000.</summary>
    public int MinCounter { get; init; } = 5_000;

    /// <summary>Highest counter the server picks for a puzzle. Default 10000.</summary>
    public int MaxCounter { get; init; } = 10_000;

    /// <summary>How long a puzzle can be solved and sent back, in minutes. Default 10.</summary>
    public int LifetimeMinutes { get; init; } = 10;
}

/// <summary>The "Captcha:Turnstile" section.</summary>
public sealed class TurnstileOptions
{
    /// <summary>Public: the page renders the widget with it.</summary>
    public string SiteKey { get; init; } = "";

    /// <summary>Secret: keep it in user secrets or an environment variable, never in appsettings.json.</summary>
    public string SecretKey { get; init; } = "";
}
