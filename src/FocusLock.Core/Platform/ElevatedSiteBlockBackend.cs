using System.ComponentModel;
using System.Diagnostics;
using FocusLock.Core.Abstractions;
using FocusLock.Core.Websites;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Platform;

/// <summary>
/// Runs FocusLock's own exe elevated (<c>--site-block apply|clear</c>) for every change and maps its exit code.
/// Each change shows the Windows permission prompt (UAC).
/// </summary>
public sealed class ElevatedSiteBlockBackend(ILogger<ElevatedSiteBlockBackend> logger, IAppEnvironment environment, IFileSystem fileSystem) : ISiteBlockBackend
{
    public Task<SiteBlockResult> ApplyAsync(IReadOnlyList<string> domains, CancellationToken cancellationToken) =>
        domains.Count == 0 ? ClearAsync(cancellationToken) : RunAsync(new SiteBlockCommand(true, domains), cancellationToken);

    public Task<SiteBlockResult> ClearAsync(CancellationToken cancellationToken) => RunAsync(new SiteBlockCommand(false, []), cancellationToken);

    /// <summary>True when the policy backup exists (policies still overridden) or the hosts file has a FocusLock section.</summary>
    public bool IsBlockPresent()
    {
        try
        {
            if (fileSystem.FileExists(SiteBlockPaths.BackupFile(environment.ProgramDataDirectory)))
            {
                return true;
            }

            var hostsPath = SiteBlockPaths.HostsFile(environment.WindowsDirectory);
            return fileSystem.FileExists(hostsPath) && HostsFileEditor.ContainsBlock(fileSystem.ReadAllText(hostsPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not check whether a website block is in place");
            return false;
        }
    }

    /// <summary>Maps a failure to start the helper: a declined UAC prompt is <see cref="SiteBlockResult.UserCancelled"/>.</summary>
    internal static SiteBlockResult MapStartFailure(Exception exception) =>
        exception is Win32Exception { NativeErrorCode: SiteBlockExitCodes.UacCancelledWin32Error } ? SiteBlockResult.UserCancelled : SiteBlockResult.Failed;

    private async Task<SiteBlockResult> RunAsync(SiteBlockCommand command, CancellationToken cancellationToken)
    {
        var verb = command.IsApply ? SiteBlockCommand.ApplyVerb : SiteBlockCommand.ClearVerb;
        Process? process;
        try
        {
            process = Process.Start(new ProcessStartInfo(environment.ExecutablePath, command.ToArguments())
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            });
        }
        catch (Exception ex)
        {
            var result = MapStartFailure(ex);
            if (result == SiteBlockResult.UserCancelled)
            {
                logger.LogInformation("Website block {Verb}: the permission prompt was declined", verb);
            }
            else
            {
                logger.LogError(ex, "Website block {Verb}: could not start the helper", verb);
            }

            return result;
        }

        if (process is null)
        {
            logger.LogError("Website block {Verb}: the helper did not start", verb);
            return SiteBlockResult.Failed;
        }

        using (process)
        {
            try
            {
                // On cancellation the elevated helper keeps running and finishes on its own; the caller gets OperationCanceledException.
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                var exitCode = process.ExitCode;
                var result = SiteBlockExitCodes.ToResult(exitCode, command.IsApply);
                if (result == SiteBlockResult.Failed)
                {
                    logger.LogWarning("Website block {Verb} failed with exit code {ExitCode}", verb, exitCode);
                }
                else
                {
                    logger.LogInformation("Website block {Verb} done ({Count} domains)", verb, command.Domains.Count);
                }

                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Website block {Verb}: could not read the helper's result", verb);
                return SiteBlockResult.Failed;
            }
        }
    }
}
