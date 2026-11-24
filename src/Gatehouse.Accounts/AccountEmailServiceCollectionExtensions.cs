using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Gatehouse.Accounts;

/// <summary>Adds the sender of Identity's account emails to the services.</summary>
public static class AccountEmailServiceCollectionExtensions
{
    /// <summary>
    /// Reads the "Email" section, registers <see cref="IEmailDelivery"/> (SMTP, log or none, by
    /// <c>Email:Delivery</c>) and replaces Identity's email sender with <see cref="AccountEmailSender{TUser}"/>. The
    /// order against <c>AddIdentityApiEndpoints</c> does not matter. Wrong settings stop the app at startup.
    /// </summary>
    /// <typeparam name="TUser">The Identity user class.</typeparam>
    /// <typeparam name="TComposer">The app's class that builds the email (subject and body) from the kind and the link.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration that holds the "Email" section.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddGatehouseAccountEmails<TUser, TComposer>(
        this IServiceCollection services, IConfiguration configuration)
        where TUser : class
        where TComposer : class, IAccountEmailComposer<TUser>
    {
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .Validate<IHostEnvironment>(
                (o, environment) => o.Delivery != EmailDelivery.Log || environment.IsDevelopment(),
                "Email:Delivery 'Log' is allowed in Development only.")
            .Validate(
                o => o.Delivery == EmailDelivery.None
                     || (Uri.TryCreate(o.FrontendBaseUrl, UriKind.Absolute, out var uri)
                         && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)),
                "Email:FrontendBaseUrl must be an absolute URL.")
            .Validate(
                o => o.Delivery != EmailDelivery.Smtp
                     || (!string.IsNullOrWhiteSpace(o.Smtp.Host)
                         && !string.IsNullOrWhiteSpace(o.FromAddress)
                         && o.Smtp.Port is >= 1 and <= 65535),
                "Email:Smtp:Host, Email:Smtp:Port and Email:FromAddress must be set for SMTP delivery.")
            .ValidateOnStart();

        services.AddSingleton<IEmailDelivery>(sp =>
            sp.GetRequiredService<IOptions<EmailOptions>>().Value.Delivery switch
            {
                EmailDelivery.Smtp => ActivatorUtilities.CreateInstance<SmtpEmailDelivery>(sp),
                EmailDelivery.Log => ActivatorUtilities.CreateInstance<LogEmailDelivery>(sp),
                _ => ActivatorUtilities.CreateInstance<NoEmailDelivery>(sp),
            });

        // The composer is scoped: the sender makes a scope per email.
        services.AddScoped<IAccountEmailComposer<TUser>, TComposer>();

        // Replaces Identity's default sender (which sends nothing), whether it was registered before or after this call.
        // Singleton: MapIdentityApi resolves it once, from the root provider.
        services.AddSingleton<AccountEmailSender<TUser>>();
        services.RemoveAll<IEmailSender<TUser>>();
        services.AddSingleton<IEmailSender<TUser>>(sp => sp.GetRequiredService<AccountEmailSender<TUser>>());

        return services;
    }
}
