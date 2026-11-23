# Gatehouse

Gatehouse is a set of small open-source NuGet packages for ASP.NET Core sign-up and sign-in. Each package does one job and depends on as little as possible. They are extracted from two real apps of the same author, so they are built for production use, not as samples.

| Package | What it does |
|---|---|
| [`Gatehouse.Captcha`](src/Gatehouse.Captcha/README.md) | Self-hosted ALTCHA proof of work and Cloudflare Turnstile behind one interface, with an endpoint filter for minimal APIs. |

## Where it comes from

The code comes from two apps by the same author:

- [What2Eat](https://getwhat2eat.com), a web app that suggests recipes from what is in your fridge.
- [MDBNavigator](https://github.com/KalliMan/MDBNavigator), an open-source database navigator.

## License

MIT, see [LICENSE](LICENSE).

Gatehouse is not an official ALTCHA or Cloudflare product.
