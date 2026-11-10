using System.ComponentModel;
using System.Windows;
using FocusLock.App.ViewModels;
using FocusLock.Core.Common;
using FocusLock.Core.Settings;
using Microsoft.Extensions.Logging;

namespace FocusLock.App.Services;

/// <summary>
/// The taskbar-corner icon, and the window's hide-to-tray behavior while a session runs.
/// Windows Forms is used only for NotifyIcon; there is no Forms UI.
/// </summary>
public sealed class TrayIconService(
    SessionController controller,
    SettingsService settings,
    ToastNotifier toasts,
    ActiveSessionViewModel activeSession,
    ILogger<TrayIconService> logger) : IDisposable
{
    private System.Windows.Forms.NotifyIcon? _icon;
    private System.Windows.Forms.ToolStripMenuItem? _endItem;
    private Window? _window;
    private bool _hideHintShown;
    private bool _quitting;

    public void Initialize(Window window)
    {
        _window = window;

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open FocusLock", null, (_, _) => ShowWindow());
        _endItem = new System.Windows.Forms.ToolStripMenuItem("End session", null, (_, _) => EndSession()) { Enabled = false };
        menu.Items.Add(_endItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, async (_, _) => await QuitAsync());

        _icon = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "FocusLock",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => ShowWindow();

        window.Closing += OnWindowClosing;
        window.StateChanged += OnWindowStateChanged;
        controller.PropertyChanged += OnControllerPropertyChanged;
    }

    public void ShowWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public void Dispose()
    {
        if (_icon is not null)
        {
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }
    }

    private void EndSession()
    {
        if (!controller.IsRunning)
        {
            return;
        }

        if (settings.Current.StrictMode)
        {
            ShowWindow();
            activeSession.OpenGate();
        }
        else
        {
            _ = controller.EndEarlyAsync();
        }
    }

    private async Task QuitAsync()
    {
        if (controller.IsRunning && settings.Current.StrictMode)
        {
            ShowWindow();
            activeSession.OpenGate();
            return;
        }

        _quitting = true;
        if (controller.IsRunning)
        {
            try
            {
                await controller.EndEarlyAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ending the session while quitting failed");
            }
        }

        Application.Current.Shutdown();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_quitting)
        {
            return;
        }

        if (controller.IsRunning)
        {
            e.Cancel = true;
            HideToTray();
        }
        else
        {
            _quitting = true;
            Application.Current.Shutdown();
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (_window?.WindowState == WindowState.Minimized && controller.IsRunning)
        {
            _window.WindowState = WindowState.Normal;
            HideToTray();
        }
    }

    private void HideToTray()
    {
        _window?.Hide();
        if (!_hideHintShown)
        {
            _hideHintShown = true;
            toasts.Show("Still focusing", "FocusLock keeps blocking from the corner of the taskbar.");
        }
    }

    private void OnControllerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SessionController.State) || _icon is null)
        {
            return;
        }

        var state = controller.State;
        _icon.Text = state.IsRunning ? $"FocusLock – {TimeText.Duration(state.RemainingInPhase)} left" : "FocusLock";
        if (_endItem is not null)
        {
            _endItem.Enabled = state.IsRunning;
        }

        // The session is over: bring the window back so the summary is seen.
        if (!state.IsRunning && _window?.IsVisible == false && !_quitting)
        {
            ShowWindow();
        }
    }

    private static System.Drawing.Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/FocusLock.ico"));
        return new System.Drawing.Icon(resource!.Stream);
    }
}
