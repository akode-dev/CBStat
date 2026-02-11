using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using Akode.CBStat.Models;

namespace Akode.CBStat.Services.Providers;

public class CodexUsageProvider : IUsageProvider
{
    private const string UsageEndpoint = "https://chatgpt.com/backend-api/wham/usage";

    private readonly HttpClient _httpClient;

    public string ProviderId => "codex";

    public CodexUsageProvider(HttpClient httpClient)
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
                return CreateError("Credentials not found. Run `codex` to authenticate.");
            }

            var result = await FetchUsageAsync(credentials.AccessToken, ct);

            // If unauthorized, try CLI refresh and retry once
            if (result.Error?.Contains("Unauthorized") == true)
            {
                if (await TryCliRefreshAsync(ct))
                {
                    credentials = await LoadCredentialsAsync(ct);
                    if (credentials != null)
                    {
                        return await FetchUsageAsync(credentials.AccessToken, ct);
                    }
                }
            }

            return result;
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

    private static async Task<bool> TryCliRefreshAsync(CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "codex",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null)
                return false;

            await process.WaitForExitAsync(cts.Token);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<UsageData> FetchUsageAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UsageEndpoint);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("User-Agent", "CBStat");

        using var response = await _httpClient.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
            response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            return CreateError("Unauthorized. Run `codex` to re-authenticate.");
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return ParseUsageResponse(json);
    }

    private UsageData ParseUsageResponse(JsonElement json)
    {
        UsageWindow? primary = null;
        UsageWindow? secondary = null;

        if (json.TryGetProperty("rate_limit", out var rateLimit))
        {
            if (rateLimit.TryGetProperty("primary_window", out var primaryWindow))
            {
                primary = ParseWindowSnapshot(primaryWindow);
            }

            if (rateLimit.TryGetProperty("secondary_window", out var secondaryWindow))
            {
                secondary = ParseWindowSnapshot(secondaryWindow);
            }
        }

        return new UsageData
        {
            Provider = ProviderId,
            Session = primary,
            Weekly = secondary,
            FetchedAt = DateTime.UtcNow
        };
    }

    private static UsageWindow? ParseWindowSnapshot(JsonElement element)
    {
        if (!element.TryGetProperty("used_percent", out var usedPercentProp))
            return null;

        var usedPercent = usedPercentProp.GetInt32();
        DateTime? resetAt = null;
        int windowMinutes = 0;

        if (element.TryGetProperty("reset_at", out var resetAtProp))
        {
            var resetAtUnix = resetAtProp.GetInt64();
            resetAt = DateTimeOffset.FromUnixTimeSeconds(resetAtUnix).UtcDateTime;
        }

        if (element.TryGetProperty("limit_window_seconds", out var windowSecondsProp))
        {
            windowMinutes = windowSecondsProp.GetInt32() / 60;
        }

        return new UsageWindow
        {
            Used = usedPercent,
            Limit = 100,
            WindowMinutes = windowMinutes,
            ResetAt = resetAt
        };
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

            if (root.TryGetProperty("OPENAI_API_KEY", out var apiKeyProp))
            {
                var apiKey = apiKeyProp.GetString()?.Trim();
                if (!string.IsNullOrEmpty(apiKey))
                {
                    return new ProviderCredentials(apiKey);
                }
            }

            if (root.TryGetProperty("tokens", out var tokens))
            {
                if (tokens.TryGetProperty("access_token", out var accessTokenProp))
                {
                    var accessToken = accessTokenProp.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        string? refreshToken = null;
                        if (tokens.TryGetProperty("refresh_token", out var refreshTokenProp))
                        {
                            refreshToken = refreshTokenProp.GetString();
                        }
                        return new ProviderCredentials(accessToken, refreshToken);
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetCredentialsPath()
    {
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME")?.Trim();
        if (!string.IsNullOrEmpty(codexHome))
        {
            return Path.Combine(codexHome, "auth.json");
        }

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

        return Path.Combine(homeDir, ".codex", "auth.json");
    }

    private UsageData CreateError(string message) => new()
    {
        Provider = ProviderId,
        Error = message,
        FetchedAt = DateTime.UtcNow
    };
}
