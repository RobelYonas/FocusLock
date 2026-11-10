using System.Windows;
using System.Windows.Threading;
using FocusLock.App.Services;
using FocusLock.App.ViewModels;
using FocusLock.App.Views;
using FocusLock.Core;
using FocusLock.Core.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FocusLock.App;

/// <summary>Composition root: single-instance guard, service container, main window and tray icon.</summary>
public partial class App : Application
{
    private const string MutexName = @"Local\FocusLock.SingleInstance";
    private const string ShowSignalName = @"Local\FocusLock.ShowWindow";

    private ServiceProvider? _services;
    private ILogger<App>? _logger;
    private Mutex? _singleInstance;
    private EventWaitHandle? _showSignal;
    private RegisteredWaitHandle? _showSignalWait;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Only one FocusLock at a time. A second start just brings the first one's window back.
        _singleInstance = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
        if (!isFirstInstance)
        {
            _showSignal.Set();
            Shutdown();
            return;
        }

        _services = ConfigureServices();
        _logger = _services.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("FocusLock {Version} starting", typeof(App).Assembly.GetName().Version);
        DispatcherUnhandledException += OnUnhandledException;

        var shell = _services.GetRequiredService<ShellViewModel>();
        var window = _services.GetRequiredService<MainWindow>();
        var tray = _services.GetRequiredService<TrayIconService>();
        var toasts = _services.GetRequiredService<ToastNotifier>();
        tray.Initialize(window);
        toasts.Activated += (_, _) => tray.ShowWindow();

        var startMinimized = e.Args.Any(arg => string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase));
        if (!startMinimized)
        {
            window.Show();
        }

        _showSignalWait = ThreadPool.RegisterWaitForSingleObject(
            _showSignal,
            (_, _) => Dispatcher.BeginInvoke(tray.ShowWindow),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        await shell.InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.LogInformation("FocusLock exiting");
        _showSignalWait?.Unregister(null);
        _showSignal?.Dispose();
        _singleInstance?.Dispose();
        _services?.Dispose();
        base.OnExit(e);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unexpected error");
        e.Handled = true;
        MessageBox.Show(
            "Something went wrong. FocusLock will keep running, and the details are in the log file.",
            "FocusLock",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddFocusLockCore();
        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddRollingFile();
        });

        services.AddSingleton<IUiDispatcher, WpfDispatcher>();
        services.AddSingleton<IFilePicker, WpfFilePicker>();
        services.AddSingleton<SessionController>();
        services.AddSingleton<ToastNotifier>();
        services.AddSingleton<TrayIconService>();

        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<ActiveSessionViewModel>();
        services.AddSingleton<BlocklistViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
