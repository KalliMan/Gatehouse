# Gatehouse.Accounts

Sign-in with Google, Microsoft, Facebook and GitHub for ASP.NET Core Identity (find, link or create the account, safely), and the sender of Identity's account emails with links to your frontend, over SMTP. Built for minimal APIs and single-page apps.

It targets .NET 10. Besides the ASP.NET Core shared framework it depends on `Microsoft.AspNetCore.Authentication.Google`, `.MicrosoftAccount` and `.Facebook`, `AspNet.Security.OAuth.GitHub` and `MailKit`.

## Install

```powershell
dotnet add package Gatehouse.Accounts
```

## Setup

Register Identity first (`AddIdentity` or `AddIdentityApiEndpoints`), then the package. `TUser` is your Identity user class.

```csharp
builder.Services.AddIdentityApiEndpoints<AppUser>(options => options.SignIn.RequireConfirmedEmail = true)
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddGatehouseExternalSignIn<AppUser, AppExternalHooks>(builder.Configuration);
builder.Services.AddGatehouseAccountEmails<AppUser, AppEmailComposer>(builder.Configuration);

app.MapGroup("/api/auth").MapIdentityApi<AppUser>();
app.MapExternalProviders();     // GET /api/auth/providers                  ["Google", "GitHub"]
app.MapExternalSignIn<AppUser>(); // GET /api/auth/external/{provider}?returnTo=
                                  // GET /api/auth/external/callback?returnTo=
```

The page links the sign-in button to `/api/auth/external/google?returnTo=/recipes` (a normal link, not a fetch). The browser goes to Google, comes back, and ends on `returnTo` signed in, or on `/login?error=...` when something is wrong.

### The hooks

Your app makes the new user object and can refuse accounts. Everything else has a default.

```csharp
public sealed class AppExternalHooks(AppDbContext db) : ExternalAccountHooks<AppUser>
{
    // A new, unsaved user. The package saves it and confirms its email through UserManager.
    public override AppUser CreateUser(ExternalAccount account) =>
        new() { UserName = account.Email, Email = account.Email, DisplayName = account.Name };

    // Optional. Default: true. False = "sign-up closed".
    public override ValueTask<bool> AllowNewAccountAsync(ExternalAccount account, CancellationToken ct) =>
        ValueTask.FromResult(true);

    // Optional. Default: true. False = the account is disabled.
    public override ValueTask<bool> CanSignInAsync(AppUser user, CancellationToken ct) =>
        ValueTask.FromResult(!user.IsDisabled);

    // Optional. Count the sign-in, store the time. An exception here is logged and does not stop the sign-in.
    public override ValueTask OnSignedInAsync(AppUser user, bool newAccount, CancellationToken ct) =>
        ValueTask.CompletedTask;
}
```

The hooks class is created once per request, so it can take scoped services (a database context).

### The email composer

The package decides which email is needed and what its link is. Your app writes it: subject, HTML and text, in the user's language.

```csharp
public sealed class AppEmailComposer(IHttpContextAccessor http) : IAccountEmailComposer<AppUser>
{
    public Task<EmailMessage?> ComposeAsync(AppUser user, AccountEmail email, CancellationToken ct)
    {
        // email.Kind: ConfirmEmail, ConfirmEmailChange or ResetPassword
        // email.To:   the address to send to (for an email change: the new address)
        // email.Link: the absolute frontend link
        // Return null to send nothing (for example when your daily limit is reached).
        var subject = email.Kind == AccountEmailKind.ResetPassword ? "Reset your password" : "Confirm your email";
        return Task.FromResult<EmailMessage?>(new EmailMessage(
            email.To, subject, $"<p><a href=\"{email.Link}\">Open the link</a></p>", email.Link));
    }
}
```

One composer is created per email, in its own scope. `IHttpContextAccessor` still gives the request (for example `Accept-Language`) when there is one.

`EmailMessage` can also carry a different sender (`From`) and extra headers (`Headers`, for example `List-Unsubscribe`).

### Sending your own emails

`IEmailDelivery` is registered by the package. Inject it to send newsletters, alerts or notices over the same settings:

```csharp
await delivery.SendAsync(new EmailMessage("ann@example.com", "Subject", "<p>Hi</p>", "Hi"), ct);
```

### Your own register or change-email endpoint

Inject `AccountEmailSender<AppUser>` and call `SendConfirmationAsync(user, email, userId, code, changedEmail)`. The `code` is Base64Url-encoded like Identity's own endpoints expect. Like all sender methods it never throws: a failed send is logged.

## Configuration

Providers (a provider is on only when both keys are set; keep the secret in user secrets or an environment variable):

| Key | Meaning |
|---|---|
| `Authentication:Google:ClientId`, `ClientSecret` | Google |
| `Authentication:Microsoft:ClientId`, `ClientSecret` | Microsoft |
| `Authentication:Facebook:ClientId`, `ClientSecret` | Facebook |
| `Authentication:GitHub:ClientId`, `ClientSecret` | GitHub (scope `user:email`) |

Email, section `Email`:

| Key | Default | Meaning |
|---|---|---|
| `Delivery` | `None` | `None` (nothing is sent, a warning is logged per email), `Log` (Development only: the email, with address and link, goes to the log) or `Smtp`. |
| `FrontendBaseUrl` | empty | Where links point, for example `https://app.example`. Required unless `Delivery` is `None`. |
| `ConfirmEmailPath` | `confirm-email` | Frontend page that confirms an email. |
| `ResetPasswordPath` | `reset-password` | Frontend page that sets a new password. |
| `FromAddress` | empty | Sender address. Required for `Smtp`. |
| `FromName` | empty | Sender name. |
| `Smtp:Host` | empty | Required for `Smtp`. |
| `Smtp:Port` | `587` | `465` = TLS from the start, anything else = STARTTLS. |
| `Smtp:Username`, `Smtp:Password` | empty | Sign-in to the server; no user name = no sign-in. |

A wrong setting stops the app at startup. `EmailOptions.Enabled` is true when `Delivery` is not `None`; use it for `SignIn.RequireConfirmedEmail`.

Options in code (all optional):

```csharp
builder.Services.AddGatehouseExternalSignIn<AppUser, AppExternalHooks>(builder.Configuration, options =>
{
    options.LoginPath = "/login";                                          // where errors go
    options.CallbackPathPrefix = "/api/auth/signin-";                      // + google, microsoft, facebook, github
    options.UnconfirmedAccountWithSameEmail = UnconfirmedAccountRule.TakeOver;   // or Refuse
    options.ConfirmUnverifiedEmailOfNewAccounts = false;
});
```

The group returned by `MapExternalSignIn` and the builder returned by `MapExternalProviders` take your own rate limit, captcha or other conventions. The prefix of `MapExternalSignIn` (default `/api/auth/external`) is a parameter.

## Callback URLs to register with the providers

Register `https://{your host}` plus:

| Provider | Redirect URI |
|---|---|
| Google | `/api/auth/signin-google` |
| Microsoft | `/api/auth/signin-microsoft` |
| Facebook | `/api/auth/signin-facebook` |
| GitHub | `/api/auth/signin-github` |

(These follow `CallbackPathPrefix`.) In development with a frontend dev server, proxy `/api` to the API so the provider returns to the same host the page is on.

## The sign-in rules

When the provider sends the user back:

1. **Already linked** to this provider: signs in. A disabled account (`CanSignInAsync` false) is refused, a locked-out account is refused, and an account whose email is still unconfirmed may sign in (the provider vouches for the person).
2. **The provider must send an email address**, else `external-no-email`.
3. **An account with this email exists:**
   - it is linked only when the provider **verified** the email; otherwise `external-email-in-use`. Without that check, anyone could take over an account by putting its email on a provider account;
   - a disabled account is refused, a locked-out account is refused;
   - if the account's email was never confirmed, the option `UnconfirmedAccountWithSameEmail` decides. `TakeOver` (default): someone else may have registered the address first, so the verified owner gets the account, its password is removed and the email is confirmed. `Refuse`: `external-email-in-use`.
4. **No account:** `AllowNewAccountAsync` may refuse (`external-sign-up-closed`). Otherwise `CreateUser` makes the user, the package saves it and confirms its email when the provider verified it (or when `ConfirmUnverifiedEmailOfNewAccounts` is true).
5. **The provider is linked** to the account and the user is signed in with Identity's normal session cookie.

Which providers count as verified: Google when its `email_verified` flag is true, and GitHub (its `user:email` scope returns only the primary verified address). Microsoft and Facebook never count: Microsoft work accounts can carry addresses nobody checked.

The return address is checked on every redirect (`ReturnUrl.Sanitize`): only paths on your own site are accepted.

## The error codes

The sign-in page receives `/login?error={code}&returnTo=...`:

| Code | When |
|---|---|
| `external-cancelled` | The user cancelled at the provider (no `returnTo`). |
| `external-failed` | Anything else went wrong, or the provider is unknown or switched off. |
| `external-no-email` | The provider sent no email address. |
| `external-email-in-use` | An account with this email exists and cannot be linked (see the rules). |
| `external-locked-out` | The account is locked out. |
| `external-disabled` | `CanSignInAsync` returned false. |
| `external-sign-up-closed` | `AllowNewAccountAsync` returned false. |

## The links in emails

Identity builds links to its own endpoints. The package turns them into links to your frontend pages:

| Email | Link | The page then calls |
|---|---|---|
| `ConfirmEmail` | `{FrontendBaseUrl}/confirm-email?userId=...&code=...` | `GET /api/auth/confirmEmail?userId=...&code=...` |
| `ConfirmEmailChange` | `.../confirm-email?userId=...&code=...&changedEmail=...` | `GET /api/auth/confirmEmail?userId=...&code=...&changedEmail=...` |
| `ResetPassword` | `{FrontendBaseUrl}/reset-password?email=...&code=...` | `POST /api/auth/resetPassword` with `email`, `resetCode` (the `code`) and `newPassword` |

## What the app still does itself

- Identity options (password rules, lockout, unique email), the session cookie and bearer tokens.
- The email texts and design, the language of each email, and any daily limit (return `null` from the composer).
- Rate limiting and captcha on the sign-in endpoints (use [`Gatehouse.Captcha`](https://www.nuget.org/packages/Gatehouse.Captcha) for the captcha).
- Account-page endpoints: linking or unlinking a provider, setting a password.

## Limits

- Real provider sign-ins and real SMTP are not covered by the automated tests (they need registered apps and a server); the rules and the emails are.
- `Email:Delivery` `Log` writes addresses and links to the log. It is refused outside Development.

## License

MIT.
