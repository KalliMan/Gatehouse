using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gatehouse.Accounts.Tests;

/// <summary>Identity on an in-memory database plus the package, without a web server. One per test.</summary>
internal sealed class IdentityFixture : IAsyncDisposable
{
    public const string Password = "Passw0rd!x";

    public IdentityFixture(Action<ExternalSignInOptions>? configure = null)
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddHttpContextAccessor();
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddIdentity<TestUser, IdentityRole<Guid>>(o => o.SignIn.RequireConfirmedEmail = true)
            .AddEntityFrameworkStores<TestDbContext>()
            .AddDefaultTokenProviders();
        services.AddSingleton<TestHooksState>();
        services.AddGatehouseExternalSignIn<TestUser, TestHooks>(
            new ConfigurationBuilder().Build(), configure);
        Provider = services.BuildServiceProvider();
    }

    public ServiceProvider Provider { get; }

    public TestHooksState State => Provider.GetRequiredService<TestHooksState>();

    /// <summary>A request scope with an HTTP context, which Identity's SignInManager needs.</summary>
    public IdentityScope OpenScope() => new(Provider.CreateAsyncScope());

    public ValueTask DisposeAsync() => Provider.DisposeAsync();

    public sealed class IdentityScope : IAsyncDisposable
    {
        private readonly AsyncServiceScope _scope;

        public IdentityScope(AsyncServiceScope scope)
        {
            _scope = scope;
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
                new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        }

        public ExternalSignIn<TestUser> SignIn => _scope.ServiceProvider.GetRequiredService<ExternalSignIn<TestUser>>();

        public UserManager<TestUser> Users => _scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();

        /// <summary>Makes a user the way an app would: through the user manager.</summary>
        public async Task<TestUser> CreateUserAsync(
            string email, bool confirmed = true, string? password = null, bool disabled = false, bool lockedOut = false)
        {
            var user = new TestUser { UserName = email, Email = email, EmailConfirmed = confirmed, IsDisabled = disabled };
            var result = password is null ? await Users.CreateAsync(user) : await Users.CreateAsync(user, password);
            Assert.True(result.Succeeded);
            if (lockedOut)
            {
                Assert.True((await Users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddDays(1))).Succeeded);
            }

            return user;
        }

        public ValueTask DisposeAsync() => _scope.DisposeAsync();
    }
}

internal static class Infos
{
    /// <summary>What the provider's callback leaves behind, made by hand.</summary>
    public static ExternalLoginInfo Make(
        string provider, string key, string? email, string? name = null, string? emailVerified = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, key) };
        if (email is not null)
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }
        if (name is not null)
        {
            claims.Add(new Claim(ClaimTypes.Name, name));
        }
        if (emailVerified is not null)
        {
            claims.Add(new Claim(ExternalProviderSetup.EmailVerifiedClaim, emailVerified));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, provider));
        return new ExternalLoginInfo(principal, provider, key, provider);
    }
}
