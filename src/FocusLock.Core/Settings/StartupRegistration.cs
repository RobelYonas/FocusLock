using System.Security;
using FocusLock.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Settings;

/// <summary>Adds or removes the "start FocusLock when I sign in" entry (HKCU Run key).</summary>
public sealed class StartupRegistration(IRegistry registry, IAppEnvironment environment, ILogger<StartupRegistration> logger)
{
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "FocusLock";
    public const string MinimizedArgument = "--minimized";

    public bool IsRegistered => registry.GetString(RegistryRoot.CurrentUser, RunKey, ValueName) is not null;

    public void Apply(bool enabled)
    {
        try
        {
            if (enabled)
            {
                registry.SetString(RegistryRoot.CurrentUser, RunKey, ValueName, $"\"{environment.ExecutablePath}\" {MinimizedArgument}");
            }
            else
            {
                registry.DeleteValue(RegistryRoot.CurrentUser, RunKey, ValueName);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            logger.LogError(ex, "Updating the start-at-sign-in entry failed");
        }
    }
}
