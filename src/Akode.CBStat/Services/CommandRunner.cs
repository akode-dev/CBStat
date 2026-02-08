using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Akode.CBStat.Services;

public class CommandRunner : ICommandRunner
{
    private readonly TimeSpan _timeout;

    public CommandRunner(int timeoutSeconds = 30)
    {
        _timeout = TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds));
    }

    public async Task<CommandResult> ExecuteAsync(string command, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows: execute via WSL
            psi.FileName = "wsl";
            psi.ArgumentList.Add("--");
            psi.ArgumentList.Add("bash");
            psi.ArgumentList.Add("-ic");
            psi.ArgumentList.Add(command);
        }
        else
        {
            // Linux/macOS: direct execution
            psi.FileName = "bash";
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(command);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        Process? process = null;
        try
        {
            process = new Process { StartInfo = psi };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token);

            var output = await outputTask;
            var error = await errorTask;

            return new CommandResult
            {
                Success = process.ExitCode == 0,
                Output = output,
                Error = error,
                ExitCode = process.ExitCode
            };
        }
        catch (OperationCanceledException)
        {
            return new CommandResult
            {
                Success = false,
                Error = "Command timed out",
                ExitCode = -1
            };
        }
        catch (Exception ex)
        {
            return new CommandResult
            {
                Success = false,
                Error = ex.Message,
                ExitCode = -1
            };
        }
        finally
        {
            if (process != null)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch { /* ignore */ }
                process.Dispose();
            }
        }
    }
}
