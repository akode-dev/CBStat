using Akode.CBStat.Models;

namespace Akode.CBStat.Services.Providers;

public interface IUsageProvider
{
    string ProviderId { get; }
    Task<UsageData> GetUsageAsync(CancellationToken ct = default);
}
