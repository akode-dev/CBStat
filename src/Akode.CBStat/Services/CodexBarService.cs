using System.Text.Json;
using Akode.CBStat.Models;

namespace Akode.CBStat.Services;

public class CodexBarService
{
    private readonly ICommandRunner _runner;

    public CodexBarService(ICommandRunner runner)
    {
        _runner = runner;
    }

    public async Task<UsageData> GetUsageAsync(string provider, CancellationToken ct = default)
    {
        try
        {
            var normalized = ProviderConstants.ValidateAndNormalize(provider);
            var source = ProviderConstants.GetSource(normalized);
            var verboseFlag = normalized == "gemini" ? " --verbose" : "";

            var result = await _runner.ExecuteAsync(
                $"codexbar usage --provider {normalized} --format json --source {source}{verboseFlag}", ct);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
            {
                return new UsageData
                {
                    Provider = provider,
                    Error = string.IsNullOrWhiteSpace(result.Error)
                        ? "Failed to fetch data"
                        : result.Error.Trim(),
                    FetchedAt = DateTime.UtcNow
                };
            }

            return ParseJson(result.Output, provider);
        }
        catch (Exception ex)
        {
            return new UsageData
            {
                Provider = provider,
                Error = ex.Message,
                FetchedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<List<UsageData>> GetAllUsageAsync(CancellationToken ct = default)
    {
        var results = new List<UsageData>();
        foreach (var provider in ProviderConstants.GetDefaultProviders())
        {
            results.Add(await GetUsageAsync(provider, ct));
        }
        return results;
    }

    private static UsageData ParseJson(string json, string provider)
    {
        try
        {
            if (json.TrimStart().StartsWith('['))
            {
                var dtos = JsonSerializer.Deserialize(json, JsonContext.Default.ListUsageDataDto);
                if (dtos?.Count > 0)
                    return dtos[0].ToUsageData();
            }
            else
            {
                var dto = JsonSerializer.Deserialize(json, JsonContext.Default.UsageDataDto);
                if (dto != null)
                    return dto.ToUsageData();
            }
        }
        catch { /* fall through */ }

        return new UsageData
        {
            Provider = provider,
            Error = "Failed to parse response",
            FetchedAt = DateTime.UtcNow
        };
    }
}
