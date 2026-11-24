namespace Gatehouse.Accounts;

/// <summary>
/// Where the browser goes after signing in. Only paths on this site are allowed, so a link can't send a freshly
/// signed-in user to another site (open redirect).
/// </summary>
public static class ReturnUrl
{
    /// <summary>Checks a return address that came from a request.</summary>
    /// <param name="value">The address from the request, or null.</param>
    /// <returns>
    /// <paramref name="value"/> when it is a same-site path: it starts with '/', not "//", and has no backslash,
    /// whitespace or control character. Otherwise "/".
    /// </returns>
    public static string Sanitize(string? value)
        => value is { Length: > 0 }
            && value[0] == '/'
            // "//host" and "/\host" are other sites to a browser, and browsers drop tabs and line breaks
            // ("/\t/host" becomes "//host").
            && !value.StartsWith("//", StringComparison.Ordinal)
            && !value.Any(c => c == '\\' || char.IsControl(c) || char.IsWhiteSpace(c))
            ? value
            : "/";
}
