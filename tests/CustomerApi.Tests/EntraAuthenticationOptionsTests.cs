using CustomerApi.Authentication;

namespace CustomerApi.Tests;

public sealed class EntraAuthenticationOptionsTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("http://entra.test", false)]
    [InlineData("not-a-uri", false)]
    [InlineData("https://entra.test/tenant/v2.0", true)]
    public void IsValidAuthority_ValidatesExpectedFormats(string? authority, bool expected)
    {
        Assert.Equal(expected, EntraAuthenticationOptions.IsValidAuthority(authority));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("api://customer-api", true)]
    [InlineData("3f2504e0-4f89-11d3-9a0c-0305e82c3301", true)]
    public void IsValidAudience_ValidatesExpectedFormats(string? audience, bool expected)
    {
        Assert.Equal(expected, EntraAuthenticationOptions.IsValidAudience(audience));
    }
}
