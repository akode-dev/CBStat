using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using Akode.CBStat.Models;

namespace Akode.CBStat.Services.Providers;

public class ClaudeUsageProvider : IUsageProvider
{
    private const string UsageEndpoint = "https://api.anthropic.com/api/oauth/usage";
    private const string TokenRefreshEndpoint = "https://platform.claude.com/v1/oauth/token";
    private const string OAuthClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";
    private const string BetaHeader = "oauth-2025-04-20";

    private readonly HttpClient _httpClient;

    public string ProviderId => "claude";

    public ClaudeUsageProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UsageData> GetUsageAsync(CancellationToken ct = default)
    {
        try
        {
            var credentials = await LoadCredentialsAsync(ct);
            if (credentials == null)
            {
                return CreateError("Credentials not found. Run `claude` to authenticate.");
            }

            var accessToken = credentials.AccessToken;

            if (credentials.IsExpired && !string.IsNullOrEmpty(credentials.RefreshToken))
            {
                var refreshed = await RefreshTokenAsync(credentials.RefreshToken, ct);
                if (refreshed != null)
                {
                    accessToken = refreshed.AccessToken;
                }
                else
                {
                    return CreateError("Token expired. Run `claude` to re-authenticate.");
                }
            }

            return await FetchUsageAsync(accessToken, ct);
        }
        catch (HttpRequestException ex)
        {
            return CreateError($"Network error: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return CreateError("Request timed out");
        }
        catch (Exception ex)
        {
            return CreateError($"Error: {ex.Message}");
        }
    }

    private async Task<UsageData> FetchUsageAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UsageEndpoint);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Headers.Add("anthropic-beta", BetaHeader);
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("User-Agent", "CBStat");

        using var response = await _httpClient.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return CreateError("Unauthorized. Run `claude` to re-authenticate.");
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return ParseUsageResponse(json);
    }

    private UsageData ParseUsageResponse(JsonElement json)
    {
        UsageWindow? session = null;
        UsageWindow? weekly = null;
        UsageWindow? tertiary = null;

        if (json.TryGetProperty("five_hour", out var fiveHour))
        {
            session = ParseUsageWindow(fiveHour, 300);
        }

        if (json.TryGetProperty("seven_day", out var sevenDay))
        {
            weekly = ParseUsageWindow(sevenDay, 10080);
        }

        if (json.TryGetProperty("seven_day_sonnet", out var sonnet))
        {
            tertiary = ParseUsageWindow(sonnet, 10080);
        }
        else if (json.TryGetProperty("seven_day_opus", out var opus))
        {
            tertiary = ParseUsageWindow(opus, 10080);
        }

        return new UsageData
        {
            Provider = ProviderId,
            Session = session,
            Weekly = weekly,
            Tertiary = tertiary,
            FetchedAt = DateTime.UtcNow
        };
    }

    private static UsageWindow? ParseUsageWindow(JsonElement element, int windowMinutes)
    {
        if (!element.TryGetProperty("utilization", out var utilizationProp))
            return null;

        var utilization = utilizationProp.GetDouble();
        DateTime? resetAt = null;

        if (element.TryGetProperty("resets_at", out var resetsAtProp))
        {
            var resetsAtStr = resetsAtProp.GetString();
            if (!string.IsNullOrEmpty(resetsAtStr) && DateTime.TryParse(resetsAtStr, out var parsed))
            {
                resetAt = parsed.ToUniversalTime();
            }
        }

        return new UsageWindow
        {
            Used = (int)(utilization * 100),
            Limit = 100,
            WindowMinutes = windowMinutes,
            ResetAt = resetAt
        };
    }

    private async Task<ProviderCredentials?> RefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = OAuthClientId
            });

            using var response = await _httpClient.PostAsync(TokenRefreshEndpoint, content, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

            if (json.TryGetProperty("access_token", out var accessTokenProp))
            {
                var accessToken = accessTokenProp.GetString() ?? "";
                var newRefreshToken = refreshToken;
                DateTime? expiresAt = null;

                if (json.TryGetProperty("refresh_token", out var refreshProp))
                {
                    newRefreshToken = refreshProp.GetString() ?? refreshToken;
                }

                if (json.TryGetProperty("expires_in", out var expiresProp))
                {
                    var expiresIn = expiresProp.GetInt32();
                    expiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
                }

                return new ProviderCredentials(accessToken, newRefreshToken, expiresAt);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<ProviderCredentials?> LoadCredentialsAsync(CancellationToken ct)
    {
        var credentialsPath = GetCredentialsPath();
        if (string.IsNullOrEmpty(credentialsPath) || !File.Exists(credentialsPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(credentialsPath, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("claudeAiOauth", out var oauth))
                return null;

            string? accessToken = null;
            string? refreshToken = null;
            DateTime? expiresAt = null;

            if (oauth.TryGetProperty("accessToken", out var accessTokenProp))
            {
                accessToken = accessTokenProp.GetString()?.Trim();
            }

            if (string.IsNullOrEmpty(accessToken))
                return null;

            if (oauth.TryGetProperty("refreshToken", out var refreshTokenProp))
            {
                refreshToken = refreshTokenProp.GetString();
            }

            if (oauth.TryGetProperty("expiresAt", out var expiresAtProp))
            {
                var expiresAtMs = expiresAtProp.GetDouble();
                expiresAt = DateTimeOffset.FromUnixTimeMilliseconds((long)expiresAtMs).UtcDateTime;
            }

            return new ProviderCredentials(accessToken, refreshToken, expiresAt);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetCredentialsPath()
    {
        string homeDir;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        else
        {
            homeDir = Environment.GetEnvironmentVariable("HOME") ?? "";
        }

        if (string.IsNullOrEmpty(homeDir))
            return null;

        return Path.Combine(homeDir, ".claude", ".credentials.json");
    }

    private UsageData CreateError(string message) => new()
    {
        Provider = ProviderId,
        Error = message,
        FetchedAt = DateTime.UtcNow
    };
}
