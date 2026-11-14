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
        controller.WebsiteNotice += (_, notice) => dispatcher.Post(() => OnWebsiteNotice(notice));
        controller.PhaseChanged += (_, phase) => dispatcher.Post(() => OnPhaseChanged(controller, phase));
        controller.Completed += (_, summary) => dispatcher.Post(() => OnCompleted(summary));
    }

    /// <summary>Raised when the person clicks a toast; the main window should come back.</summary>
    public event EventHandler? Activated;

    public void Show(string title, string body)
    {
        if (_settings.Current.ShowNotifications)
        {
            ShowAlways(title, body);
        }
    }

    /// <summary>For problems the person must know about, even with notifications off.</summary>
    private void ShowAlways(string title, string body)
    {
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

    private void OnWebsiteNotice(WebsiteNotice notice)
    {
        switch (notice.Kind)
        {
            case WebsiteNoticeKind.Blocked:
                Show("Websites blocked", "Blocked sites won't open until your focus time ends.");
                break;
            case WebsiteNoticeKind.RestartBrowsers:
                Show("Restart your browser", $"Restart {WebsiteText.JoinNames(notice.Browsers)} so tabs that are already open are blocked too.");
                break;
            case WebsiteNoticeKind.PermissionDeclined:
                ShowAlways("Websites aren't blocked this time", "The permission prompt was declined. Games are still blocked.");
                break;
            case WebsiteNoticeKind.Failed:
                ShowAlways("Websites aren't blocked this time", "Something went wrong. Games are still blocked.");
                break;
            case WebsiteNoticeKind.StillBlocked:
                ShowAlways("Websites are still blocked", "FocusLock couldn't unblock them. It will try again the next time it starts.");
                break;
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
