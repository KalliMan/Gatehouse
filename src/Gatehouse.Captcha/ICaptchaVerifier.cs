namespace Gatehouse.Captcha;

/// <summary>Checks the token the browser got from the captcha widget.</summary>
public interface ICaptchaVerifier
{
    /// <summary>False with Captcha:Provider None: nothing asks for a token.</summary>
    bool Enabled { get; }

    /// <summary>True only for a valid token that wasn't used before. A token works once.</summary>
    /// <param name="token">The token the browser sent.</param>
    /// <param name="ct">Cancels the check.</param>
    Task<bool> VerifyAsync(string? token, CancellationToken ct = default);
}
