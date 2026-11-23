namespace Gatehouse.Captcha.Tests;

/// <summary>A clock the tests can move.</summary>
internal sealed class FakeTime(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
