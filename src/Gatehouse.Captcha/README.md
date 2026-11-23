# Gatehouse.Captcha

Captcha checks for ASP.NET Core minimal APIs: self-hosted [ALTCHA](https://altcha.org) proof of work (the PBKDF2 format of the altcha 3.x widget) and Cloudflare Turnstile behind one interface, with an endpoint filter. Not an official ALTCHA or Cloudflare product.

It targets .NET 10 and has no NuGet dependencies, only the ASP.NET Core shared framework.

## Install

```powershell
dotnet add package Gatehouse.Captcha
```

## Setup

```csharp
builder.Services.AddGatehouseCaptcha(builder.Configuration);

app.MapCaptchaSettings();    // GET /api/captcha            which captcha the page should show
app.MapCaptchaChallenge();   // GET /api/captcha/challenge  a new ALTCHA puzzle (404 unless Provider is Altcha)

app.MapPost("/api/register", Register).RequireCaptcha();
```

`RequireCaptcha()` works on a single endpoint or on a route group. `RequireCaptcha(anonymousOnly: true)` does not ask signed-in callers.

The challenge endpoint has no rate limit of its own. Add one to the builder it returns, for example `app.MapCaptchaChallenge().RequireRateLimiting("captcha")`.

## Configuration

Section `Captcha`:

| Key | Default | Meaning |
|---|---|---|
| `Provider` | `None` | `None`, `Altcha` or `Turnstile`. With `None` nothing asks for a token. |
| `Altcha:Cost` | `5000` | PBKDF2 iterations per attempt (at least 1). |
| `Altcha:MinCounter` | `5000` | Lowest counter the server picks for a puzzle (at least 0). |
| `Altcha:MaxCounter` | `10000` | Highest counter (not below `MinCounter`, below 2147483647). |
| `Altcha:LifetimeMinutes` | `10` | How long a puzzle can be solved and sent back (at least 1). |
| `Turnstile:SiteKey` | empty | Public key. Required with Turnstile. |
| `Turnstile:SecretKey` | empty | Secret key, keep it in user secrets or an environment variable. Required with Turnstile. |

A wrong setting throws an `InvalidOperationException` at startup, naming the key.

## What the browser sends

- **ALTCHA:** point the widget's `challengeurl` at the challenge endpoint. The widget solves the puzzle in the browser; send its payload (the base64 text it produces) in the `Captcha-Token` request header.
- **Turnstile:** render the widget with the site key from the settings endpoint and send the token it gives you in the `Captcha-Token` header.

Each token works once. The header name can be changed with `CaptchaEndpointOptions.TokenHeader`.

## Changing the failure answer

By default a failed check answers a ProblemDetails 400 with the extension `code` set to `Captcha.Required` (no token) or `Captcha.Failed` (a wrong, expired or used token). To answer differently:

```csharp
builder.Services.AddGatehouseCaptcha(builder.Configuration, options =>
{
    options.TokenHeader = "X-Captcha";
    options.OnFailure = (http, failure) => failure == CaptchaFailure.Missing
        ? Results.Problem(statusCode: 400, title: "Captcha needed")
        : Results.Problem(statusCode: 400, title: "Captcha failed");
});
```

`RequireCaptcha` adds only the filter and no OpenAPI metadata, because the package can't know the status code of your answer. To run the check yourself, call `CaptchaCheck.RunAsync(httpContext)`: it returns `null` when the request passed and otherwise the answer to return.

## Limits

- **One app instance.** The ALTCHA signing key and the list of used puzzles live in memory. A restart makes open puzzles invalid (the widget fetches a new one), and several instances would need a shared key and a shared store of used puzzles.
- No rate limiting inside the package.
- The automated tests use their own solver, not the altcha widget. With the real altcha 3.x widget the package has been tried in the What2Eat app.

## License

MIT.
