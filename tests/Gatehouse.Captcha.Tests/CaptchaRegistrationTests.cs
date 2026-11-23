using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Gatehouse.Captcha.Tests;

public class CaptchaRegistrationTests
{
    private static ServiceCollection Register(
        Dictionary<string, string?> config, Action<CaptchaEndpointOptions>? configureEndpoints = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config).Build();
        var services = new ServiceCollection();
        services.AddGatehouseCaptcha(configuration, configureEndpoints);
        return services;
    }

    [Fact]
    public void Without_configuration_no_captcha_is_used()
    {
        using var provider = Register([]).BuildServiceProvider();

        var verifier = provider.GetRequiredService<ICaptchaVerifier>();

        Assert.IsType<NoCaptcha>(verifier);
        Assert.False(verifier.Enabled);
    }

    [Fact]
    public void Provider_None_registers_no_captcha()
    {
        using var provider = Register(new() { ["Captcha:Provider"] = "None" }).BuildServiceProvider();

        Assert.IsType<NoCaptcha>(provider.GetRequiredService<ICaptchaVerifier>());
        Assert.Null(provider.GetService<AltchaCaptcha>());
    }

    [Fact]
    public void Provider_Altcha_registers_one_shared_instance()
    {
        using var provider = Register(new() { ["Captcha:Provider"] = "Altcha" }).BuildServiceProvider();

        var verifier = provider.GetRequiredService<ICaptchaVerifier>();

        Assert.IsType<AltchaCaptcha>(verifier);
        Assert.True(verifier.Enabled);
        Assert.Same(verifier, provider.GetRequiredService<AltchaCaptcha>());
    }

    [Fact]
    public void Provider_Turnstile_registers_the_turnstile_verifier()
    {
        using var provider = Register(new()
        {
            ["Captcha:Provider"] = "Turnstile",
            ["Captcha:Turnstile:SiteKey"] = "site",
            ["Captcha:Turnstile:SecretKey"] = "secret",
        }).BuildServiceProvider();

        var verifier = provider.GetRequiredService<ICaptchaVerifier>();

        Assert.IsType<TurnstileVerifier>(verifier);
        Assert.True(verifier.Enabled);
        Assert.Null(provider.GetService<AltchaCaptcha>());
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("site", null)]
    [InlineData(null, "secret")]
    [InlineData("  ", "secret")]
    [InlineData("site", "  ")]
    public void Turnstile_without_both_keys_throws(string? siteKey, string? secretKey)
    {
        var config = new Dictionary<string, string?>
        {
            ["Captcha:Provider"] = "Turnstile",
            ["Captcha:Turnstile:SiteKey"] = siteKey,
            ["Captcha:Turnstile:SecretKey"] = secretKey,
        };

        var ex = Assert.Throws<InvalidOperationException>(() => Register(config));

        Assert.Contains("Captcha:Turnstile:SiteKey", ex.Message);
        Assert.Contains("Captcha:Turnstile:SecretKey", ex.Message);
    }

    [Theory]
    [InlineData("Cost", "0", "Captcha:Altcha:Cost")]
    [InlineData("MinCounter", "-1", "Captcha:Altcha:MinCounter")]
    [InlineData("MaxCounter", "4999", "Captcha:Altcha:MaxCounter")] // below the default MinCounter of 5000
    [InlineData("MaxCounter", "2147483647", "Captcha:Altcha:MaxCounter")]
    [InlineData("LifetimeMinutes", "0", "Captcha:Altcha:LifetimeMinutes")]
    public void A_bad_altcha_value_throws_and_names_the_key(string key, string value, string expectedKey)
    {
        var config = new Dictionary<string, string?>
        {
            ["Captcha:Provider"] = "Altcha",
            [$"Captcha:Altcha:{key}"] = value,
        };

        var ex = Assert.Throws<InvalidOperationException>(() => Register(config));

        Assert.Contains(expectedKey, ex.Message);
    }

    [Fact]
    public void Altcha_values_are_not_checked_for_other_providers()
    {
        var config = new Dictionary<string, string?> { ["Captcha:Altcha:Cost"] = "0" };

        using var provider = Register(config).BuildServiceProvider();

        Assert.IsType<NoCaptcha>(provider.GetRequiredService<ICaptchaVerifier>());
    }

    [Fact]
    public void The_altcha_defaults_are_the_old_fixed_values()
    {
        using var provider = Register(new() { ["Captcha:Provider"] = "Altcha" }).BuildServiceProvider();

        var altcha = provider.GetRequiredService<IOptions<CaptchaOptions>>().Value.Altcha;

        Assert.Equal(5_000, altcha.Cost);
        Assert.Equal(5_000, altcha.MinCounter);
        Assert.Equal(10_000, altcha.MaxCounter);
        Assert.Equal(10, altcha.LifetimeMinutes);
    }

    [Fact]
    public void Endpoint_options_have_defaults_and_can_be_changed()
    {
        using var defaults = Register([]).BuildServiceProvider();
        Assert.Equal("Captcha-Token", defaults.GetRequiredService<CaptchaEndpointOptions>().TokenHeader);

        using var changed = Register([], o => o.TokenHeader = "X-Captcha").BuildServiceProvider();
        Assert.Equal("X-Captcha", changed.GetRequiredService<CaptchaEndpointOptions>().TokenHeader);
    }

    [Fact]
    public void An_existing_time_provider_is_kept()
    {
        var time = new FakeTime(DateTimeOffset.UnixEpoch);
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(time);
        services.AddGatehouseCaptcha(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        Assert.Same(time, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void The_system_time_provider_is_added_when_none_exists()
    {
        using var provider = Register([]).BuildServiceProvider();

        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }
}
