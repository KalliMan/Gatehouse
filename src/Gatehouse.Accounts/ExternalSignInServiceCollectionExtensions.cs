using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gatehouse.Accounts;

/// <summary>Adds provider sign-in to the services.</summary>
public static class ExternalSignInServiceCollectionExtensions
{
    /// <summary>
    /// Adds the providers that are configured ("Authentication:{Google|Microsoft|Facebook|GitHub}:ClientId" and
    /// "ClientSecret") and the rules that find, link or create the account. Identity must already be registered
    /// (<c>AddIdentity</c> or <c>AddIdentityApiEndpoints</c>).
    /// </summary>
    /// <typeparam name="TUser">The Identity user class.</typeparam>
    /// <typeparam name="THooks">The app's hooks: how a new user is made and whether an account may sign in.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration that holds the "Authentication" section.</param>
    /// <param name="configure">Changes the default <see cref="ExternalSignInOptions"/>.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="InvalidOperationException"><c>LoginPath</c> or <c>CallbackPathPrefix</c> does not start with '/'.</exception>
    public static IServiceCollection AddGatehouseExternalSignIn<TUser, THooks>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<ExternalSignInOptions>? configure = null)
        where TUser : class
        where THooks : ExternalAccountHooks<TUser>
    {
        var options = new ExternalSignInOptions();
        configure?.Invoke(options);
        RequireLeadingSlash(options.LoginPath, nameof(ExternalSignInOptions.LoginPath));
        RequireLeadingSlash(options.CallbackPathPrefix, nameof(ExternalSignInOptions.CallbackPathPrefix));

        services.Configure<ExternalSignInOptions>(o => configure?.Invoke(o));

        var names = ExternalProviderSetup.Add(services.AddAuthentication(), configuration, options);
        services.AddSingleton(new ExternalProviderList(names));
        services.AddScoped<ExternalAccountHooks<TUser>, THooks>();
        services.AddScoped<ExternalSignIn<TUser>>();
        return services;
    }

    private static void RequireLeadingSlash(string value, string property)
    {
        if (!value.StartsWith('/'))
        {
            throw new InvalidOperationException($"ExternalSignInOptions.{property} must start with '/'.");
        }
    }
}
