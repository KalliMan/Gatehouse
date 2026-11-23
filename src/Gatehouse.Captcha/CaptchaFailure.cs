namespace Gatehouse.Captcha;

/// <summary>Why a request didn't pass the captcha check.</summary>
public enum CaptchaFailure
{
    /// <summary>The request had no token.</summary>
    Missing,

    /// <summary>The token was wrong, expired or already used.</summary>
    Invalid,
}
