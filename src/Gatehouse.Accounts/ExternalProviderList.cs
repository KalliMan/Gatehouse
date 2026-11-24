namespace Gatehouse.Accounts;

/// <summary>The providers that are switched on, because both <c>ClientId</c> and <c>ClientSecret</c> are configured.</summary>
public sealed class ExternalProviderList
{
    internal ExternalProviderList(IEnumerable<string> names) => Names = names.Order(StringComparer.Ordinal).ToList();

    /// <summary>The names of the switched-on providers in ordinal order, for example <c>["GitHub", "Google"]</c>.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>Finds a provider by a name from a URL such as "google".</summary>
    /// <param name="name">The name, in any letter case.</param>
    /// <returns>The provider's name (for example "Google"), or null when it is not switched on.</returns>
    public string? Find(string name) =>
        Names.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
}
