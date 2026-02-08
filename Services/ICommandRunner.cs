namespace CLIStat.Services;

public interface ICommandRunner
{
    Task<CommandResult> ExecuteAsync(string command, CancellationToken ct = default);
}

public record CommandResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public int ExitCode { get; init; }
}
