namespace CustomerApi.Authentication;

public sealed class EntraAuthenticationOptions
{
    public const string SectionName = "Entra";

    public string? Authority { get; init; }

    public string? Audience { get; init; }

    public static bool IsValidAuthority(string? authority) =>
        Uri.TryCreate(authority, UriKind.Absolute, out var uri) &&
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    public static bool IsValidAudience(string? audience)
    {
        if (string.IsNullOrWhiteSpace(audience))
        {
            return false;
        }

        return Guid.TryParse(audience, out _) ||
               Uri.TryCreate(audience, UriKind.Absolute, out _);
    }
}
