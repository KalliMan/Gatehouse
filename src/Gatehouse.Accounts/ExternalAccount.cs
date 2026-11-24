namespace Gatehouse.Accounts;

/// <summary>What a provider told us about the person who signed in.</summary>
/// <param name="Provider">The provider's name, for example "Google" (see <see cref="ExternalProviders"/>).</param>
/// <param name="Email">The email address the provider sent.</param>
/// <param name="Name">The provider's display name, trimmed; null when empty.</param>
/// <param name="EmailVerified">True when the provider vouches that this person owns the address.</param>
public sealed record ExternalAccount(string Provider, string Email, string? Name, bool EmailVerified);
