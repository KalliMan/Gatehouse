namespace Gatehouse.Captcha;

/// <summary>Captcha:Provider None. Nothing asks for a token, so VerifyAsync is never meant to pass.</summary>
internal sealed class NoCaptcha : ICaptchaVerifier
{
    public bool Enabled => false;

    public Task<bool> VerifyAsync(string? token, CancellationToken ct = default) => Task.FromResult(false);
}
