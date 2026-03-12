using System.Globalization;
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

    // Cache last successful result to return during rate limiting
    private static UsageData? _lastSuccessfulResult;
    private static DateTime _rateLimitedUntil = DateTime.MinValue;

    // Claude Code version detection for User-Agent
    private const string DefaultVersion = "2.1.0";
    private static readonly Lazy<string> _claudeCodeVersion = new(DetectClaudeCodeVersion);

    public string ProviderId => "claude";

    public ClaudeUsageProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UsageData> GetUsageAsync(CancellationToken ct = default)
    {
        try
        {
            // If we're rate limited, return cached result or wait message
            if (DateTime.UtcNow < _rateLimitedUntil)
            {
                if (_lastSuccessfulResult != null)
                {
                    // Return cached data with a note
                    return _lastSuccessfulResult with { Error = null };
                }
                var waitSeconds = (int)(_rateLimitedUntil - DateTime.UtcNow).TotalSeconds;
                return CreateError($"Rate limited. Retry in {waitSeconds}s");
            }

            var credentials = await LoadCredentialsAsync(ct);
            if (credentials == null)
            {
                return CreateError("Credentials not found. Run `claude` to authenticate.");
            }

            var accessToken = credentials.AccessToken;

            // Try API refresh if token is expired
            if (credentials.IsExpired && !string.IsNullOrEmpty(credentials.RefreshToken))
            {
                var refreshed = await RefreshTokenAsync(credentials.RefreshToken, ct);
                if (refreshed != null)
                {
                    accessToken = refreshed.AccessToken;
                }
                else
                {
                    // API refresh failed, try CLI refresh
                    if (await TryCliRefreshAsync(ct))
                    {
                        credentials = await LoadCredentialsAsync(ct);
                        if (credentials != null)
                            accessToken = credentials.AccessToken;
                    }
                    else
                    {
                        return CreateError("Token expired. Run `claude` to re-authenticate.");
                    }
                }
            }

            // Re-read credentials right before API call in case Claude Code updated the token
            var freshCredentials = await LoadCredentialsAsync(ct);
            if (freshCredentials != null)
                accessToken = freshCredentials.AccessToken;

            var result = await FetchUsageAsync(accessToken, ct);

            // Cache successful result
            if (result.Error == null)
            {
                _lastSuccessfulResult = result;
            }

            // If error (unauthorized or network), try with fresh credentials once more
            if (result.Error != null)
            {
                // Wait briefly and re-read credentials - Claude Code may have just refreshed
                await Task.Delay(500, ct);
                freshCredentials = await LoadCredentialsAsync(ct);
                if (freshCredentials != null && freshCredentials.AccessToken != accessToken)
                {
                    var retryResult = await FetchUsageAsync(freshCredentials.AccessToken, ct);
                    if (retryResult.Error == null)
                        return retryResult;
                }

                // If still failing and it's auth-related, try CLI refresh
                if (result.Error.Contains("Unauthorized") && await TryCliRefreshAsync(ct))
                {
                    freshCredentials = await LoadCredentialsAsync(ct);
                    if (freshCredentials != null)
                    {
                        return await FetchUsageAsync(freshCredentials.AccessToken, ct);
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
        // Try multiple strategies to refresh the token
        var strategies = new[]
        {
            ("claude", "-p \".\" --max-turns 1"),  // Real API call triggers refresh
            ("claude", "auth status"),             // Check auth status
        };

        foreach (var (fileName, args) in strategies)
        {
            if (await TryRunCliAsync(fileName, args, ct))
                return true;
        }

        return false;
    }

    private static async Task<bool> TryRunCliAsync(string fileName, string arguments, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30)); // Longer timeout for actual API call

            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(homeDir))
                homeDir = Environment.GetEnvironmentVariable("HOME") ?? ".";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = homeDir  // Avoid "trust this folder" prompt
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null)
                return false;

            // Close stdin to prevent hanging on input prompts
            process.StandardInput.Close();

            await process.WaitForExitAsync(cts.Token);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string DetectClaudeCodeVersion()
    {
        try
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(homeDir))
                homeDir = Environment.GetEnvironmentVariable("HOME") ?? ".";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "claude",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = homeDir
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null)
                return DefaultVersion;

            // 5-second timeout
            if (!process.WaitForExit(5000))
            {
                try { process.Kill(); } catch { }
                return DefaultVersion;
            }

            if (process.ExitCode != 0)
                return DefaultVersion;

            var output = process.StandardOutput.ReadToEnd().Trim();
            return ParseClaudeVersionOutput(output);
        }
        catch
        {
            return DefaultVersion;
        }
    }

    /// <summary>
    /// Parses the output of "claude --version" to extract version number.
    /// Example: "2.1.74 (Claude Code)" -> "2.1.74"
    /// </summary>
    internal static string ParseClaudeVersionOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return DefaultVersion;

        var spaceIndex = output.IndexOf(' ');
        var version = spaceIndex > 0 ? output[..spaceIndex] : output.Trim();

        // Validate version format (e.g., "2.1.74" or "2.1")
        if (System.Text.RegularExpressions.Regex.IsMatch(version, @"^\d+\.\d+"))
            return version;

        return DefaultVersion;
    }

    private async Task<UsageData> FetchUsageAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UsageEndpoint);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Headers.Add("anthropic-beta", BetaHeader);
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("User-Agent", $"claude-code/{_claudeCodeVersion.Value}");

        using var response = await _httpClient.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return CreateError("Unauthorized. Run `claude auth login`");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            // Set rate limit backoff - check Retry-After header or default to 60s
            var retryAfter = 60;
            if (response.Headers.TryGetValues("Retry-After", out var values))
            {
                var retryValue = values.FirstOrDefault();
                if (int.TryParse(retryValue, out var parsed))
                    retryAfter = parsed;
            }
            _rateLimitedUntil = DateTime.UtcNow.AddSeconds(retryAfter);

            // Return cached result if available
            if (_lastSuccessfulResult != null)
            {
                return _lastSuccessfulResult with { Error = null };
            }
            return CreateError($"Rate limited. Retry in {retryAfter}s");
        }

        if (!response.IsSuccessStatusCode)
        {
            return CreateError($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
        }

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
            if (!string.IsNullOrEmpty(resetsAtStr) &&
                DateTimeOffset.TryParse(resetsAtStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                resetAt = parsed.UtcDateTime;
            }
        }

        // API returns utilization as percentage (e.g., 16.0 = 16%)
        // not as fraction (0.16), so don't multiply by 100
        var usedPercent = utilization >= 1.0 ? (int)utilization : (int)(utilization * 100);

        return new UsageWindow
        {
            Used = usedPercent,
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
