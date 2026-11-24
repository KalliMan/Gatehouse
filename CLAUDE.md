# CLAUDE.md

Gatehouse: small open-source NuGet packages for ASP.NET Core sign-up and sign-in, extracted from two apps of the same owner (What2Eat and MDBNavigator). MIT license. .NET 10.

Packages:

- `Gatehouse.Captcha` (first): self-hosted ALTCHA proof of work and Cloudflare Turnstile behind one interface, with an endpoint filter.
- `Gatehouse.Accounts` (0.1: provider sign-in and account emails): Google, Microsoft, Facebook and GitHub sign-in on top of ASP.NET Core Identity, and the sender of Identity's account emails.

## How to work with the owner

- Experienced developer, new to publishing NuGet packages. Explain decisions briefly and plainly, with short examples when useful. Keep answers concise.
- For anything non-trivial: propose a short plan and **wait for OK** before creating or changing files.
- **Never `git commit`, `git push` or `git tag` before the owner has reviewed the changes and explicitly said so.** A pushed tag `v*` publishes to nuget.org and cannot be taken back.
- Never commit secrets.

## Plans (Opus plans, Sonnet implements)

- Plans live in `docs/plans/<version>-<topic>.md` (e.g. `0.1-captcha.md`) with these sections, in order: Goal, Decisions, Files, Patterns to copy, Contracts, Edge cases & don'ts, Steps, Verification.
- Implementer: follow the plan. If something is missing, wrong, or would force a design decision, **stop and ask** instead of improvising.

## Commands

```powershell
dotnet build Gatehouse.slnx -c Release      # what CI runs (warnings = errors)
dotnet test Gatehouse.slnx -c Release
dotnet pack src/Gatehouse.Captcha -c Release -o artifacts   # a local .nupkg to look at
dotnet pack src/Gatehouse.Accounts -c Release -o artifacts
```

## Rules

- **This is a public package: other people's apps depend on it.**
  - Every public type and member has an XML doc comment (the Release build fails without one).
  - A change to a public signature, a config key, a default value or the wire format is a breaking change: say so in the plan and in `CHANGELOG.md`. Versions follow SemVer; below 1.0 a breaking change raises the minor version.
  - Keep the public surface small. `internal` unless an app must use it.
- **Tests are required** for every change (xUnit, in `tests/`). No test, no merge.
- **Dependencies:** `Gatehouse.Captcha` depends only on the ASP.NET Core shared framework (`FrameworkReference`), no NuGet packages. `Gatehouse.Accounts` may also depend on `Microsoft.AspNetCore.Authentication.Google`, `.MicrosoftAccount`, `.Facebook`, `AspNet.Security.OAuth.GitHub` and `MailKit`; nothing else without asking. Test projects may use packages. Versions only in `Directory.Packages.props`; permissive licenses only (MIT, Apache-2.0, BSD), no commercial tiers.
- **Security code:** compare secrets and signatures with `CryptographicOperations.FixedTimeEquals`; fail closed (an error means "not passed"); never log tokens, secrets, emails or IP addresses.
- No app-specific words (What2Eat, MDBNavigator, SPEC, phase numbers) in code or comments. The README may name the apps.
- Code style as in What2Eat: file-scoped namespaces, primary constructors, `sealed` classes, `[LoggerMessage]` for logs.
- The version comes from the package's git tag (`accounts-v0.1.0` gives Gatehouse.Accounts 0.1.0, `captcha-v0.1.1` gives Gatehouse.Captcha 0.1.1); no version number in a `.csproj`.
