using System.Windows.Threading;
using FocusLock.App.Views;
using FocusLock.Core.Abstractions;
using FocusLock.Core.Common;
using FocusLock.Core.Sessions;
using FocusLock.Core.Settings;

namespace FocusLock.App.Services;

/// <summary>
/// Small in-app notes in the corner of the screen. Used instead of Windows toasts
/// because those are hidden by Do Not Disturb, which people often turn on to focus.
/// </summary>
public sealed class ToastNotifier
{
    private static readonly TimeSpan ShowTime = TimeSpan.FromSeconds(5);

    private readonly SettingsService _settings;
    private readonly DispatcherTimer _timer;
    private ToastWindow? _window;

    public ToastNotifier(SessionController controller, SettingsService settings, IUiDispatcher dispatcher)
    {
        _settings = settings;
        _timer = new DispatcherTimer { Interval = ShowTime };
        _timer.Tick += (_, _) => HideToast();

        // The controller re-raises Core's events on the UI thread already.
        controller.LaunchBlocked += (_, notice) => dispatcher.Post(() => OnLaunchBlocked(notice));
        controller.PhaseChanged += (_, phase) => dispatcher.Post(() => OnPhaseChanged(controller, phase));
        controller.Completed += (_, summary) => dispatcher.Post(() => OnCompleted(summary));
    }

    /// <summary>Raised when the person clicks a toast; the main window should come back.</summary>
    public event EventHandler? Activated;

    public void Show(string title, string body)
    {
        if (!_settings.Current.ShowNotifications)
        {
            return;
        }

        _window ??= CreateWindow();
        _window.SetMessage(title, body);
        _window.Show();
        _timer.Stop();
        _timer.Start();
    }

    private void OnLaunchBlocked(BlockedLaunchNotice notice)
    {
        if (notice.Result == TerminateResult.Terminated)
        {
            Show($"{notice.GameName} was closed", $"Focus session active, {TimeText.Duration(notice.RemainingInPhase)} left.");
        }
        else if (notice.Result == TerminateResult.AccessDenied)
        {
            Show($"{notice.GameName} couldn't be closed", "You may need to close it yourself this time.");
        }
    }

    private void OnPhaseChanged(SessionController controller, SessionPhase phase)
    {
        if (phase == SessionPhase.Break)
        {
            Show("Break time", $"Games are allowed for the next {TimeText.Duration(controller.State.RemainingInPhase)}.");
        }
        else
        {
            Show("Back to focus", $"Games are blocked again. {TimeText.Duration(controller.State.RemainingTotal)} to go.");
        }
    }

    private void OnCompleted(SessionSummary summary)
    {
        if (!summary.EndedEarly)
        {
            Show("Session complete", $"Nice work — you focused for {TimeText.Spoken(summary.FocusedTime)}.");
        }
    }

    private ToastWindow CreateWindow()
    {
        var window = new ToastWindow();
        window.Clicked += (_, _) =>
        {
            HideToast();
            Activated?.Invoke(this, EventArgs.Empty);
        };
        return window;
    }

    private void HideToast()
    {
        _timer.Stop();
        _window?.Hide();
    }
}
