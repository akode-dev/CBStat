namespace Akode.CBStat.Models;

/// <summary>
/// Configuration for a provider.
/// </summary>
public record ProviderConfig
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int Order { get; set; }

    public static IReadOnlyList<ProviderConfig> GetDefaults() =>
    [
        new() { Id = "claude", DisplayName = "Claude", IsEnabled = true, Order = 0 },
        new() { Id = "codex", DisplayName = "Codex", IsEnabled = true, Order = 1 },
        new() { Id = "gemini", DisplayName = "Gemini", IsEnabled = true, Order = 2 }
    ];
}

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
}
