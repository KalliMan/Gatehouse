using Microsoft.Extensions.Options;

namespace Gatehouse.Captcha.Tests;

public class AltchaCaptchaTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTime _time = new(Start);

    private AltchaCaptcha Create(int lifetimeMinutes = 10) =>
        new(_time, Options.Create(AltchaSolver.FastOptions(lifetimeMinutes)));

    [Fact]
    public async Task A_solved_puzzle_passes_once()
    {
        var captcha = Create();
        var token = AltchaSolver.SolveToToken(captcha.CreateChallenge());

        Assert.True(await captcha.VerifyAsync(token));
        Assert.False(await captcha.VerifyAsync(token));
    }

    [Fact]
    public void The_challenge_uses_the_configured_values()
    {
        var challenge = Create(lifetimeMinutes: 3).CreateChallenge();

        Assert.Equal("PBKDF2/SHA-256", challenge.Parameters.Algorithm);
        Assert.Equal(1, challenge.Parameters.Cost);
        Assert.Equal(32, challenge.Parameters.KeyLength);
        Assert.Equal(32, challenge.Parameters.KeyPrefix.Length);
        Assert.Equal(Start.AddMinutes(3).ToUnixTimeSeconds(), challenge.Parameters.ExpiresAt);
    }

    [Fact]
    public async Task A_puzzle_is_refused_after_its_lifetime()
    {
        var captcha = Create();
        var token = AltchaSolver.SolveToToken(captcha.CreateChallenge());

        _time.UtcNow = Start.AddMinutes(10).AddSeconds(1);

        Assert.False(await captcha.VerifyAsync(token));
    }

    [Fact]
    public async Task A_puzzle_is_still_accepted_just_before_its_lifetime_ends()
    {
        var captcha = Create();
        var token = AltchaSolver.SolveToToken(captcha.CreateChallenge());

        _time.UtcNow = Start.AddMinutes(9).AddSeconds(58);

        Assert.True(await captcha.VerifyAsync(token));
    }

    [Fact]
    public async Task A_changed_cost_is_refused()
    {
        var captcha = Create();
        var challenge = captcha.CreateChallenge();
        var (counter, key) = AltchaSolver.Solve(challenge);
        var altered = challenge with { Parameters = challenge.Parameters with { Cost = 2 } };

        Assert.False(await captcha.VerifyAsync(AltchaSolver.ToToken(altered, counter, key)));
    }

    [Fact]
    public async Task A_changed_expiry_is_refused()
    {
        var captcha = Create();
        var challenge = captcha.CreateChallenge();
        var (counter, key) = AltchaSolver.Solve(challenge);
        var altered = challenge with
        {
            Parameters = challenge.Parameters with { ExpiresAt = challenge.Parameters.ExpiresAt + 3600 },
        };

        Assert.False(await captcha.VerifyAsync(AltchaSolver.ToToken(altered, counter, key)));
    }

    [Fact]
    public async Task A_wrong_derived_key_is_refused()
    {
        var captcha = Create();
        var challenge = captcha.CreateChallenge();
        var (counter, key) = AltchaSolver.Solve(challenge);
        var wrongKey = (key[0] == '0' ? "1" : "0") + key[1..];

        Assert.False(await captcha.VerifyAsync(AltchaSolver.ToToken(challenge, counter, wrongKey)));
    }

    [Fact]
    public async Task A_wrong_counter_is_refused()
    {
        var captcha = Create();
        var challenge = captcha.CreateChallenge();
        var (counter, key) = AltchaSolver.Solve(challenge);

        Assert.False(await captcha.VerifyAsync(AltchaSolver.ToToken(challenge, counter + 1, key)));
    }

    [Fact]
    public async Task A_negative_counter_is_refused()
    {
        var captcha = Create();
        var challenge = captcha.CreateChallenge();
        var (_, key) = AltchaSolver.Solve(challenge);

        Assert.False(await captcha.VerifyAsync(AltchaSolver.ToToken(challenge, -1, key)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("this is not base64!")]
    [InlineData("aGVsbG8=")] // base64 of "hello": not JSON
    [InlineData("e30=")] // base64 of "{}": no challenge, no solution
    public async Task Garbage_is_refused(string? token)
    {
        Assert.False(await Create().VerifyAsync(token));
    }

    [Fact]
    public async Task The_widgets_test_payload_is_refused()
    {
        var token = AltchaSolver.EncodeText("""{"challenge":null,"solution":null,"test":true}""");

        Assert.False(await Create().VerifyAsync(token));
    }

    [Fact]
    public async Task A_token_over_4096_characters_is_refused()
    {
        Assert.False(await Create().VerifyAsync(new string('A', 4097)));
    }

    [Fact]
    public async Task A_puzzle_from_another_instance_is_refused()
    {
        var other = Create();
        var token = AltchaSolver.SolveToToken(Create().CreateChallenge());

        Assert.False(await other.VerifyAsync(token));
    }

    [Fact]
    public async Task Each_puzzle_passes_on_its_own()
    {
        var captcha = Create();
        var first = AltchaSolver.SolveToToken(captcha.CreateChallenge());
        var second = AltchaSolver.SolveToToken(captcha.CreateChallenge());

        Assert.True(await captcha.VerifyAsync(first));
        Assert.True(await captcha.VerifyAsync(second));
    }
}
