using FocusLock.Core.Abstractions;
using FocusLock.Core.Blocking;
using FocusLock.Core.Detection;
using FocusLock.Core.Detection.Epic;
using FocusLock.Core.Detection.Steam;
using FocusLock.Core.Platform;
using FocusLock.Core.Processes;
using FocusLock.Core.Sessions;
using FocusLock.Core.Settings;
using FocusLock.Core.Updates;
using FocusLock.Core.Websites;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the Core services with their Windows implementations.</summary>
    public static IServiceCollection AddFocusLockCore(this IServiceCollection services)
    {
        // System adapters
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITickTimer, ThreadingTickTimer>();
        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<IRegistry, WindowsRegistry>();
        services.AddSingleton<IAppEnvironment, WindowsAppEnvironment>();
        services.AddSingleton<IProcessProvider, WindowsProcessProvider>();
        services.AddSingleton<IProcessTerminator, WindowsProcessTerminator>();

        // Settings and stats
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<IStatsStore, JsonStatsStore>();
        services.AddSingleton<StartupRegistration>();

        // Detection
        services.AddSingleton<IGameDetector, SteamGameDetector>();
        services.AddSingleton<IGameDetector, EpicGameDetector>();
        services.AddSingleton<IGameDetector, GogGameDetector>();
        services.AddSingleton<IGameDetector, UbisoftGameDetector>();
        services.AddSingleton<IGameDetector>(sp => PublisherGameDetector.Ea(sp.GetRequiredService<IRegistry>(), sp.GetRequiredService<ILogger<PublisherGameDetector>>()));
        services.AddSingleton<IGameDetector>(sp => PublisherGameDetector.BattleNet(sp.GetRequiredService<IRegistry>(), sp.GetRequiredService<ILogger<PublisherGameDetector>>()));
        services.AddSingleton<IGameDetector>(sp => PublisherGameDetector.Riot(sp.GetRequiredService<IRegistry>(), sp.GetRequiredService<ILogger<PublisherGameDetector>>()));
        services.AddSingleton<IGameDetector, LauncherDetector>();
        services.AddSingleton<IGameDetector, CustomListDetector>();
        services.AddSingleton<GameCatalog>();
        services.AddSingleton<AppChoiceFinder>();

        // Updates
        services.AddSingleton(sp => new UpdateChecker(
            new HttpClient { Timeout = TimeSpan.FromSeconds(10) },
            sp.GetRequiredService<ILogger<UpdateChecker>>()));

        // Blocking
        services.AddSingleton(ProcessWatcherOptions.Default);
        services.AddSingleton<IProcessWatcher, PollingProcessWatcher>();
        services.AddSingleton<BlockRuleEvaluator>();
        services.AddSingleton<BlockPolicyBuilder>();
        services.AddSingleton<BlockingEngine>();

        // Websites
        services.AddSingleton<ISiteBlockBackend, ElevatedSiteBlockBackend>();
        services.AddSingleton<ISitePresetCatalog, SitePresetCatalog>();
        services.AddSingleton<WebsiteBlocker>();
        services.AddSingleton<BrowserCloser>();

        // Sessions
        services.AddSingleton<FocusSessionEngine>();
        services.AddSingleton<ISessionService, InProcessSessionService>();

        return services;
    }
}
