using System.Text.Json.Serialization;

namespace Akode.CBStat.Models;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true)]
[JsonSerializable(typeof(UsageDataDto))]
[JsonSerializable(typeof(List<UsageDataDto>))]
[JsonSerializable(typeof(AppSettings))]
public partial class JsonContext : JsonSerializerContext
{
}

public record UsageDataDto
{
    public string Provider { get; init; } = string.Empty;
    public UsageDto? Usage { get; init; }
    public string? Error { get; init; }

    public UsageData ToUsageData() => new()
    {
        Provider = Provider,
        Session = Usage?.Primary?.ToUsageWindow(),
        Weekly = Usage?.Secondary?.ToUsageWindow(),
        Tertiary = Usage?.Tertiary?.ToUsageWindow(),
        Error = Error,
        FetchedAt = DateTime.UtcNow
    };
}

public record UsageDto
{
    public UsageWindowDto? Primary { get; init; }
    public UsageWindowDto? Secondary { get; init; }
    public UsageWindowDto? Tertiary { get; init; }
}

public record UsageWindowDto
{
    public double UsedPercent { get; init; }
    public int WindowMinutes { get; init; }
    public DateTime? ResetsAt { get; init; }
    public string? ResetDescription { get; init; }

    public UsageWindow ToUsageWindow() => new()
    {
        Used = (int)UsedPercent,
        Limit = 100,
        WindowMinutes = WindowMinutes,
        ResetAt = ResetsAt,
        ResetIn = ResetDescription
    };
}
