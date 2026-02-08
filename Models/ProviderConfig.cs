namespace CLIStat.Models;

/// <summary>
/// Application constants for providers.
/// </summary>
public static class ProviderConstants
{
    public static readonly IReadOnlySet<string> AllowedProviders =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "claude", "codex", "gemini" };

    public static bool IsValidProvider(string providerId)
        => !string.IsNullOrWhiteSpace(providerId) && AllowedProviders.Contains(providerId);

    public static string ValidateAndNormalize(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            throw new ArgumentException("Provider ID cannot be null or empty.", nameof(providerId));

        var normalized = providerId.Trim().ToLowerInvariant();
        if (!AllowedProviders.Contains(normalized))
            throw new ArgumentException($"Invalid provider: '{providerId}'", nameof(providerId));

        return normalized;
    }

    public static string GetSource(string providerId)
    {
        var normalized = ValidateAndNormalize(providerId);
        return normalized switch
        {
            "claude" => "oauth",
            "codex" => "cli",
            "gemini" => "cli",
            _ => throw new InvalidOperationException($"Unhandled provider: {normalized}")
        };
    }

    public static IReadOnlyList<string> GetDefaultProviders() => ["claude", "codex", "gemini"];
}
