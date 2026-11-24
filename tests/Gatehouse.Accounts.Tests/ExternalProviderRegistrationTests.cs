using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Gatehouse.Accounts.Tests;

public sealed class ExternalProviderRegistrationTests
{
    private static ServiceProvider Build(Dictionary<string, string?> config, Action<ExternalSignInOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<TestHooksState>();
        services.AddGatehouseExternalSignIn<TestUser, TestHooks>(
            new ConfigurationBuilder().AddInMemoryCollection(config).Build(), configure);
        return services.BuildServiceProvider();
    }

    private static Dictionary<string, string?> Credentials(params string[] providers)
    {
        var config = new Dictionary<string, string?>();
        foreach (var provider in providers)
        {
            config[$"Authentication:{provider}:ClientId"] = "id";
            config[$"Authentication:{provider}:ClientSecret"] = "secret";
        }

        return config;
    }

    [Fact]
    public async Task Only_providers_with_id_and_secret_are_switched_on()
    {
        var config = Credentials("Google");
        config["Authentication:Microsoft:ClientId"] = "id";   // no secret
        await using var provider = Build(config);

        Assert.Equal(["Google"], provider.GetRequiredService<ExternalProviderList>().Names);
        var schemes = await provider.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync();
        Assert.Contains(schemes, s => s.Name == "Google");
        Assert.DoesNotContain(schemes, s => s.Name == "Microsoft");
    }

    [Fact]
    public async Task Names_come_in_ordinal_order()
    {
        await using var provider = Build(Credentials("Google", "GitHub", "Facebook", "Microsoft"));

        Assert.Equal(["Facebook", "GitHub", "Google", "Microsoft"], provider.GetRequiredService<ExternalProviderList>().Names);
    }

    [Fact]
    public async Task Google_is_set_up_for_the_identity_external_cookie()
    {
        await using var provider = Build(Credentials("Google"));

        var options = provider.GetRequiredService<IOptionsMonitor<GoogleOptions>>().Get("Google");

        Assert.Equal("/api/auth/signin-google", options.CallbackPath.Value);
        Assert.Equal("Identity.External", options.SignInScheme);
        Assert.Equal(SameSiteMode.Lax, options.CorrelationCookie.SameSite);
        Assert.Equal("id", options.ClientId);
    }

    [Fact]
    public async Task A_custom_callback_prefix_is_used()
    {
        await using var provider = Build(Credentials("Google"), o => o.CallbackPathPrefix = "/auth/back-");

        var options = provider.GetRequiredService<IOptionsMonitor<GoogleOptions>>().Get("Google");

        Assert.Equal("/auth/back-google", options.CallbackPath.Value);
    }

    [Fact]
    public async Task GitHub_asks_for_the_email_scope()
    {
        await using var provider = Build(Credentials("GitHub"));

        var options = provider.GetRequiredService<IOptionsMonitor<GitHubAuthenticationOptions>>().Get("GitHub");

        Assert.Contains("user:email", options.Scope);
        Assert.Equal("/api/auth/signin-github", options.CallbackPath.Value);
    }

    [Fact]
    public async Task Find_ignores_letter_case_and_gives_null_for_unknown_names()
    {
        await using var provider = Build(Credentials("Google"));
        var list = provider.GetRequiredService<ExternalProviderList>();

        Assert.Equal("Google", list.Find("google"));
        Assert.Null(list.Find("x"));
    }

    [Theory]
    [InlineData("login", null, "LoginPath")]
    [InlineData("/login", "signin-", "CallbackPathPrefix")]
    public void A_path_without_a_leading_slash_throws(string loginPath, string? prefix, string property)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(new Dictionary<string, string?>(), o =>
        {
            o.LoginPath = loginPath;
            if (prefix is not null)
            {
                o.CallbackPathPrefix = prefix;
            }
        }));

        Assert.Contains(property, ex.Message);
    }

    [Theory]
    [InlineData("Google", "true", true)]
    [InlineData("Google", "True", true)]
    [InlineData("Google", "false", false)]
    [InlineData("Google", null, false)]
    [InlineData("GitHub", null, true)]
    [InlineData("Microsoft", "true", false)]
    [InlineData("Facebook", "true", false)]
    public void IsEmailVerified_follows_the_provider_rules(string provider, string? claim, bool expected)
    {
        var info = Infos.Make(provider, "key", "a@example.com", emailVerified: claim);

        Assert.Equal(expected, ExternalProviderSetup.IsEmailVerified(info));
    }
}
