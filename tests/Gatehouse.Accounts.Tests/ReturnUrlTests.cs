namespace Gatehouse.Accounts.Tests;

public sealed class ReturnUrlTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/a/b?c=1#d")]
    public void Same_site_paths_are_kept(string value) => Assert.Equal(value, ReturnUrl.Sanitize(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("//evil.com")]
    [InlineData("/\\evil.com")]
    [InlineData("/a\\b")]
    [InlineData("https://evil.com")]
    [InlineData("/a b")]
    [InlineData("/a\tb")]
    [InlineData("/a\u0001")]
    public void Anything_else_becomes_the_root(string? value) => Assert.Equal("/", ReturnUrl.Sanitize(value));
}
