using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OpenAgent.Providers;

/// <summary>
/// Thin abstraction over process execution so the CLI adapter is unit-testable
/// without spawning real external agents. Discovery (<see cref="CliDiscovery"/>)
/// finds the executable; the adapter drives it through this runner (spec section
/// 27: adapters drive CLIs, but the shell's tests must not depend on one being
/// installed).
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs <paramref name="fileName"/> with <paramref name="arguments"/> and
    /// captures output. Arguments are an argv list the OS joins — the prompt
    /// travels as one literal element and can never reshape the command line.
    /// </summary>
    Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}

/// <summary>The captured result of a CLI run.</summary>
public sealed record ProcessRunResult(int ExitCode, string StdOut, string StdErr);

/// <summary>Runs a CLI via <see cref="System.Diagnostics.Process"/>.</summary>
public sealed class RealProcessRunner : IProcessRunner
{
    public async Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        var start = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException($"无法启动 CLI 进程: {fileName}");

        // Read both streams concurrently, then await exit — avoids the classic
        // fill-the-buffer deadlock when stderr and stdout race.
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        return new ProcessRunResult(process.ExitCode, stdout, stderr);
    }
}
