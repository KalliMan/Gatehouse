using Microsoft.AspNetCore.Identity;

namespace Gatehouse.Accounts.Tests;

public sealed class ExternalSignInTests
{
    private const string Email = "ann@example.com";

    [Fact]
    public async Task A_linked_account_signs_in()
    {
        await using var fx = new IdentityFixture();
        await using var scope = fx.OpenScope();
        var user = await scope.CreateUserAsync(Email);
        await scope.Users.AddLoginAsync(user, new UserLoginInfo("Google", "k1", "Google"));

        var result = await scope.SignIn.SignInAsync(Infos.Make("Google", "k1", Email), default);

        Assert.Equal(ExternalSignInResult.Success, result);
        Assert.Equal([(Email, false)], fx.State.SignedIn);
    }

    [Fact]
    public async Task A_linked_but_disabled_account_is_refused()
    {
        await using var fx = new IdentityFixture();
        await using var scope = fx.OpenScope();
        var user = await scope.CreateUserAsync(Email, disabled: true);
        await scope.Users.AddLoginAsync(user, new UserLoginInfo("Google", "k1", "Google"));

        var result = await scope.SignIn.SignInAsync(Infos.Make("Google", "k1", Email), default);

        Assert.Equal(ExternalSignInResult.Disabled, result);
        Assert.Empty(fx.State.SignedIn);
    }

    [Fact]
    public async Task A_linked_but_locked_out_account_is_refused()
    {
        await using var fx = new IdentityFixture();
        await using var scope = fx.OpenScope();
        var user = await scope.CreateUserAsync(Email, lockedOut: true);
        await scope.Users.AddLoginAsync(user, new UserLoginInfo("Google", "k1", "Google"));

        var result = await scope.SignIn.SignInAsync(Infos.Make("Google", "k1", Email), default);

        Assert.Equal(ExternalSignInResult.LockedOut, result);
    }

    [Fact]
    public async Task A_linked_account_with_an_unconfirmed_email_signs_in()
    {
        await using var fx = new IdentityFixture();
        await using var scope = fx.OpenScope();
        var user = await scope.CreateUserAsync(Email, confirmed: false);
        await scope.Users.AddLoginAsync(user, new UserLoginInfo("Google", "k1", "Google"));

        var result = await scope.SignIn.SignInAsync(Infos.Make("Google", "k1", Email), default);

        Assert.Equal(ExternalSignInResult.Success, result);
        Assert.Equal([(Email, false)], fx.State.SignedIn);
    }

    [Fact]
    public async Task A_linked_locked_out_account_with_an_unconfirmed_email_is_refused()
    {
        await using var fx = new IdentityFixture();
        await using var scope = fx.OpenScope();
        var user = await scope.CreateUserAsync(Email, confirmed: false, lockedOut: true);
        await scope.Users.AddLoginAsync(user, new UserLoginInfo("Google", "k1", "Google"));

        var result = await scope.SignIn.SignInAsync(Infos.Make("Google", "k1", Email), default);

        Assert.Equal(ExternalSignInResult.LockedOut, result);
    }

    [Fact]
    public async Task No_email_claim_gives_NoEmail()
    {
        await using var fx = new IdentityFixture();
        await using var scope = fx.OpenScope();

        var result = await scope.SignIn.SignInAsync(Infos.Make("Google", "k1", email: null), default);

        Assert.Equal(ExternalSignInResult.NoEmail, result);
    }

    [Fact]
    public async Task A_new_verified_account_is_created_confirmed_and_linked()
    {
        await using var fx = new IdentityFixture();
        await using var scope = fx.OpenScope();

        var result = await scope.SignIn.SignInAsync(
            Infos.Make("Google", "k1", Email, name: "  Ann Lee  ", emailVerified: "true"), default);

        Assert.Equal(ExternalSignInResult.Success, result);
        var user = await scope.Users.FindByEmailAsync(Email);
        Assert.NotNull(user);
        Assert.True(await scope.Users.IsEmailConfirmedAsync(user));
        Assert.Equal("Ann Lee", user.DisplayName);
        var login = Assert.Single(await scope.Users.GetLoginsAsync(user));
        Assert.Equal("Google", login.LoginProvider);
        Assert.Equal("k1", login.ProviderKey);
        Assert.Equal([(Email, true)], fx.State.SignedIn);
    }

    [Fact]
    public async Task A_whitespace_name_becomes_null()
    {
        await using var fx = new IdentityFixture();
        await using var scope = fx.OpenScope();

        await scope.SignIn.SignInAsync(Infos.Make("Google", "k1", Email, name: "   ", emailVerified: "true"), default);

        var user = await scope.Users.FindByEmailAsync(Email);
        Assert.NotNull(user);
        Assert.Null(user.DisplayName);
    }

    [Fact]
    public async Task A_new_account_from_an_unverified_provider_stays_unconfirmed()
    {
        await using var fx = new IdentityFixture();
        await using var scope = fx.OpenScope();

        var result = await scope.SignIn.SignInAsync(Infos.Make("Microsoft", "k1", Email), default);

        Assert.Equal(ExternalSignInResult.Success, result);
        var user = await scope.Users.FindByEmailAsync(Email);
        Assert.NotNull(user);
        Assert.False(await scope.Users.IsEmailConfirmedAsync(user));
    }

    [Fact]
    public async Task A_new_account_from_an_unverified_provider_is_confirmed_when_the_option_says_so()
    {
        await using var fx = new IdentityFixture(o => o.ConfirmUnverifiedEmailOfNewAccounts = true);
        await using var scope = fx.OpenScope();

        var result = await scope.SignIn.SignInAsync(Infos.Make("Microsoft", "k1", Email), default);

        Assert.Equal(ExternalSignInResult.Success, result);
        var user = await scope.Users.FindByEmailAsync(Email);
        Assert.NotNull(user);
        Assert.True(await scope.Users.IsEmailConfirmedAsync(user));
    }

    [Fact]
    public async Task A_closed_sign_up_creates_nothing()
    {
        await using var fx = new IdentityFixture();
        fx.State.AllowNewAccounts = false;
        await using var scope = fx.OpenScope();

        var result = await scope.SignIn.SignInAsync(Infos.Make("Google", "k1", Email, emailVerified: "true"), default);

        Assert.Equal(ExternalSignInResult.SignUpClosed, result);
        Assert.Null(await scope.Users.FindByEmailAsync(Email));
        Assert.Empty(fx.State.SignedIn);
    }

    [Fact]
    public async Task A_confirmed_account_is_linked_by_a_verified_provider()
    {
        await using var fx = new IdentityFixture();
        await using var scope = fx.OpenScope();
        var user = await scope.CreateUserAsync(Email, password: IdentityFixture.Password);

        var result = await scope.SignIn.SignInAsync(Infos.Make("Google", "k1", Email, emailVerified: "true"), default);

        Assert.Equal(ExternalSignInResult.Success, result);
        Assert.Single(await scope.Users.GetLoginsAsync(user));
        Assert.True(await scope.Users.HasPasswordAsync(user));
        Assert.Equal([(Email, false)], fx.State.SignedIn);
    }

    [Fact]
    public async Task An_existing_account_is_not_linked_by_an_unverified_provider()
    {
        await using var fx = new IdentityFixture();
        await using var scope = fx.OpenScope();
        var user = await scope.CreateUserAsync(Email);

        var result = await scope.SignIn.SignInAsync(Infos.Make("Facebook", "k1", Email), default);

        Assert.Equal(ExternalSignInResult.EmailInUse, result);
        Assert.Empty(await scope.Users.GetLoginsAsync(user));
    }

    [Fact]
    public async Task An_unconfirmed_account_is_taken_over_by_default()
    {
        await using var fx = new IdentityFixture();
        await using var scope = fx.OpenScope();
        var user = await scope.CreateUserAsync(Email, confirmed: false, password: IdentityFixture.Password);

        var result = await scope.SignIn.SignInAsync(Infos.Make("Google", "k1", Email, emailVerified: "true"), default);

        Assert.Equal(ExternalSignInResult.Success, result);
        Assert.False(await scope.Users.HasPasswordAsync(user));
        Assert.True(await scope.Users.IsEmailConfirmedAsync(user));
        Assert.Single(await scope.Users.GetLoginsAsync(user));
    }

    [Fact]
    public async Task An_unconfirmed_account_is_refused_under_the_Refuse_rule()
    {
        await using var fx = new IdentityFixture(o => o.UnconfirmedAccountWithSameEmail = UnconfirmedAccountRule.Refuse);
        await using var scope = fx.OpenScope();
        var user = await scope.CreateUserAsync(Email, confirmed: false, password: IdentityFixture.Password);

        var result = await scope.SignIn.SignInAsync(Infos.Make("Google", "k1", Email, emailVerified: "true"), default);

        Assert.Equal(ExternalSignInResult.EmailInUse, result);
        Assert.True(await scope.Users.HasPasswordAsync(user));
        Assert.False(await scope.Users.IsEmailConfirmedAsync(user));
        Assert.Empty(await scope.Users.GetLoginsAsync(user));
    }

    [Fact]
    public async Task An_existing_disabled_account_is_refused()
    {
        await using var fx = new IdentityFixture();
        await using var scope = fx.OpenScope();
        await scope.CreateUserAsync(Email, disabled: true);

        var result = await scope.SignIn.SignInAsync(Infos.Make("Google", "k1", Email, emailVerified: "true"), default);

        Assert.Equal(ExternalSignInResult.Disabled, result);
    }

    [Fact]
    public async Task An_existing_locked_out_account_is_refused()
    {
        await using var fx = new IdentityFixture();
        await using var scope = fx.OpenScope();
        var user = await scope.CreateUserAsync(Email, lockedOut: true);

        var result = await scope.SignIn.SignInAsync(Infos.Make("Google", "k1", Email, emailVerified: "true"), default);

        Assert.Equal(ExternalSignInResult.LockedOut, result);
        Assert.Empty(await scope.Users.GetLoginsAsync(user));
    }

    [Fact]
    public async Task A_failing_hook_does_not_stop_the_sign_in()
    {
        await using var fx = new IdentityFixture();
        fx.State.ThrowOnSignedIn = true;
        await using var scope = fx.OpenScope();

        var result = await scope.SignIn.SignInAsync(Infos.Make("Google", "k1", Email, emailVerified: "true"), default);

        Assert.Equal(ExternalSignInResult.Success, result);
        Assert.NotNull(await scope.Users.FindByEmailAsync(Email));
    }
}
