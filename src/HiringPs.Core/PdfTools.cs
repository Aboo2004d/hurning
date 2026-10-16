using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

// Run document tools directly, with bounded output and a deadline; never through a shell.
public sealed class PdfTools(IConfiguration configuration)
{
    public async Task<string> RunAsync(
        string setting,
        string fallback,
        IEnumerable<string> arguments,
        CancellationToken ct
    )
    {
        var executable = configuration["Pdf:" + setting] ?? fallback;
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            },
        };
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var seconds = int.TryParse(configuration["Pdf:ProcessTimeoutSeconds"], out var configured)
            ? Math.Clamp(configured, 1, 45)
            : 45;
        timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
        try
        {
            if (!process.Start())
                throw new ContentNeedsReviewException($"PDF tool could not start: {fallback}.");
            var output = DrainAsync(process.StandardOutput, timeout.Token);
            var error = DrainAsync(process.StandardError, timeout.Token);
            var pending = new List<Task> { output, error, process.WaitForExitAsync(timeout.Token) };
            while (pending.Count > 0)
            {
                var completed = await Task.WhenAny(pending);
                await completed;
                pending.Remove(completed);
            }
            if (process.ExitCode != 0)
                throw new ContentNeedsReviewException(
                    $"PDF tool failed: {fallback} (exit {process.ExitCode}); inspect the PDF and tool installation."
                );
            return await output + "\n" + await error;
        }
        catch (Win32Exception)
        {
            throw new ContentNeedsReviewException(
                $"PDF tool is unavailable: {fallback}; configure Pdf:{setting} and executable permissions."
            );
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ContentNeedsReviewException(
                $"PDF tool timed out after {seconds} seconds: {fallback}."
            );
        }
        finally
        {
            try
            {
                if (process.Id > 0 && !process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
            }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { }
            catch (TimeoutException) { }
        }
    }

    private static async Task<string> DrainAsync(StreamReader reader, CancellationToken ct)
    {
        var buffer = new char[4096];
        var output = new StringBuilder();
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), ct)) > 0)
        {
            if (output.Length + read > 65536)
                throw new ContentNeedsReviewException(
                    "PDF tool exceeded its diagnostic output limit."
                );
            output.Append(buffer, 0, read);
        }
        return output.ToString();
    }
}
