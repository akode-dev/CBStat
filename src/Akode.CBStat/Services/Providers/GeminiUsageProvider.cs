using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Akode.CBStat.Models;

namespace Akode.CBStat.Services.Providers;

public class GeminiUsageProvider : IUsageProvider
{
    private const string QuotaEndpoint = "https://cloudcode-pa.googleapis.com/v1internal:retrieveUserQuota";
    private const string TokenRefreshEndpoint = "https://oauth2.googleapis.com/token";

    private readonly HttpClient _httpClient;

    public string ProviderId => "gemini";

    public GeminiUsageProvider(HttpClient httpClient)
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
                return CreateError("Credentials not found. Run `gemini` to authenticate.");
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
                    return CreateError("Token expired. Run `gemini` to re-authenticate.");
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
        using var request = new HttpRequestMessage(HttpMethod.Post, QuotaEndpoint);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return CreateError("Unauthorized. Run `gemini` to re-authenticate.");
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return ParseQuotaResponse(json);
    }

    private UsageData ParseQuotaResponse(JsonElement json)
    {
        if (!json.TryGetProperty("buckets", out var buckets) || buckets.ValueKind != JsonValueKind.Array)
        {
            return CreateError("Invalid quota response");
        }

        var modelQuotas = new Dictionary<string, (double remaining, DateTime? resetAt)>(StringComparer.OrdinalIgnoreCase);

        foreach (var bucket in buckets.EnumerateArray())
        {
            if (!bucket.TryGetProperty("modelId", out var modelIdProp))
                continue;
            if (!bucket.TryGetProperty("remainingFraction", out var remainingProp))
                continue;

            var modelId = modelIdProp.GetString() ?? "";
            var remaining = remainingProp.GetDouble();

            DateTime? resetAt = null;
            if (bucket.TryGetProperty("resetTime", out var resetTimeProp))
            {
                var resetTimeStr = resetTimeProp.GetString();
                if (!string.IsNullOrEmpty(resetTimeStr) && DateTime.TryParse(resetTimeStr, out var parsed))
                {
                    resetAt = parsed.ToUniversalTime();
                }
            }

            if (!modelQuotas.TryGetValue(modelId, out var existing) || remaining < existing.remaining)
            {
                modelQuotas[modelId] = (remaining, resetAt);
            }
        }

        UsageWindow? proWindow = null;
        UsageWindow? flashWindow = null;

        foreach (var (modelId, (remaining, resetAt)) in modelQuotas)
        {
            var lowerModelId = modelId.ToLowerInvariant();
            var usedPercent = (int)((1 - remaining) * 100);

            var window = new UsageWindow
            {
                Used = usedPercent,
                Limit = 100,
                WindowMinutes = 1440,
                ResetAt = resetAt
            };

            if (lowerModelId.Contains("pro"))
            {
                if (proWindow == null || usedPercent > proWindow.Used)
                    proWindow = window;
            }
            else if (lowerModelId.Contains("flash"))
            {
                if (flashWindow == null || usedPercent > flashWindow.Used)
                    flashWindow = window;
            }
        }

        return new UsageData
        {
            Provider = ProviderId,
            Session = proWindow,
            Weekly = flashWindow,
            FetchedAt = DateTime.UtcNow
        };
    }

    private async Task<ProviderCredentials?> RefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        try
        {
            var clientCreds = await GetOAuthClientCredentialsAsync();
            if (clientCreds == null)
                return null;

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientCreds.Value.clientId,
                ["client_secret"] = clientCreds.Value.clientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            });

            using var response = await _httpClient.PostAsync(TokenRefreshEndpoint, content, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

            if (json.TryGetProperty("access_token", out var accessTokenProp))
            {
                var accessToken = accessTokenProp.GetString() ?? "";
                DateTime? expiresAt = null;

                if (json.TryGetProperty("expires_in", out var expiresProp))
                {
                    var expiresIn = expiresProp.GetDouble();
                    expiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
                }

                return new ProviderCredentials(accessToken, refreshToken, expiresAt);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<(string clientId, string clientSecret)?> GetOAuthClientCredentialsAsync()
    {
        var paths = GetPossibleGeminiOAuthPaths();

        foreach (var path in paths)
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var content = await File.ReadAllTextAsync(path);
                var clientId = ExtractValue(content, @"OAUTH_CLIENT_ID\s*=\s*['""]([^'""]+)['""]");
                var clientSecret = ExtractValue(content, @"OAUTH_CLIENT_SECRET\s*=\s*['""]([^'""]+)['""]");

                if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret))
                {
                    return (clientId, clientSecret);
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static string? ExtractValue(string content, string pattern)
    {
        var match = System.Text.RegularExpressions.Regex.Match(content, pattern);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static IEnumerable<string> GetPossibleGeminiOAuthPaths()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            yield return Path.Combine(appData, "npm", "node_modules", "@google", "gemini-cli-core", "dist", "src", "code_assist", "oauth2.js");
            yield return Path.Combine(localAppData, "npm", "node_modules", "@google", "gemini-cli-core", "dist", "src", "code_assist", "oauth2.js");
        }
        else
        {
            var home = Environment.GetEnvironmentVariable("HOME") ?? "";
            yield return Path.Combine(home, ".nvm", "versions", "node", "v20.0.0", "lib", "node_modules", "@google", "gemini-cli-core", "dist", "src", "code_assist", "oauth2.js");
            yield return "/usr/local/lib/node_modules/@google/gemini-cli-core/dist/src/code_assist/oauth2.js";
            yield return "/usr/lib/node_modules/@google/gemini-cli-core/dist/src/code_assist/oauth2.js";
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

            string? accessToken = null;
            string? refreshToken = null;
            DateTime? expiresAt = null;

            if (root.TryGetProperty("access_token", out var accessTokenProp))
            {
                accessToken = accessTokenProp.GetString()?.Trim();
            }

            if (string.IsNullOrEmpty(accessToken))
                return null;

            if (root.TryGetProperty("refresh_token", out var refreshTokenProp))
            {
                refreshToken = refreshTokenProp.GetString();
            }

            if (root.TryGetProperty("expiry_date", out var expiresAtProp))
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

        return Path.Combine(homeDir, ".gemini", "oauth_creds.json");
    }

    private UsageData CreateError(string message) => new()
    {
        Provider = ProviderId,
        Error = message,
        FetchedAt = DateTime.UtcNow
    };
}
