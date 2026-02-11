namespace Akode.CBStat.Services.Providers;

public record ProviderCredentials(
    string AccessToken,
    string? RefreshToken = null,
    DateTime? ExpiresAt = null)
{
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow >= ExpiresAt.Value;
}
