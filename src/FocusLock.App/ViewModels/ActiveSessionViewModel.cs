using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLock.App.Services;
using FocusLock.Core.Abstractions;
using FocusLock.Core.Common;
using FocusLock.Core.Sessions;
using FocusLock.Core.Settings;
using Microsoft.Extensions.Logging;

namespace FocusLock.App.ViewModels;

public sealed partial class ActiveSessionViewModel : ObservableObject
{
    private readonly SessionController _controller;
    private readonly SettingsService _settings;
    private readonly IClock _clock;
    private readonly ILogger<ActiveSessionViewModel> _logger;
    private readonly StrictModeGate _gate = new();

    [ObservableProperty]
    private bool _isGateOpen;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmEndEarlyCommand))]
    private string _gateTypedText = string.Empty;

    public ActiveSessionViewModel(SessionController controller, SettingsService settings, IClock clock, ILogger<ActiveSessionViewModel> logger)
    {
        _controller = controller;
        _settings = settings;
        _clock = clock;
        _logger = logger;
        controller.PropertyChanged += OnControllerPropertyChanged;
    }

    private SessionState State => _controller.State;

    public bool IsFocus => State.Phase == SessionPhase.Focus;

    public string PhaseText => IsFocus ? "Focus" : "Break";

    public string PhaseHint => IsFocus ? "Games are closed while this runs." : "Games are allowed until the break ends.";

    public string CountdownText => TimeText.Countdown(State.RemainingInPhase);

    public string CountdownAccessibleText => $"{TimeText.Spoken(State.RemainingInPhase)} left in this {PhaseText.ToLowerInvariant()} block";

    public double Progress => State.PhaseProgress;

    public string CycleText => State.TotalCycles > 1 ? $"Block {State.Cycle} of {State.TotalCycles}" : string.Empty;

    public string BlockedText => State.BlockedAttempts switch
    {
        0 => "No game launches blocked yet",
        1 => "1 game launch blocked",
        var count => $"{count} game launches blocked",
    };

    public string EndsAtText => $"Ends around {(_clock.UtcNow + State.RemainingTotal).ToLocalTime():t}";

    public string GateSentence => StrictModeGate.Sentence;

    public string GateWaitText
    {
        get
        {
            var remaining = _gate.RemainingWait(_clock.UtcNow);
            if (remaining == TimeSpan.Zero)
            {
                return "You can end the session now.";
            }

            var seconds = (int)Math.Ceiling(remaining.TotalSeconds);
            return $"The button unlocks in {seconds} seconds — or type the sentence to end right away.";
        }
    }

    /// <summary>Shows the strict-mode panel. Also called from the tray menu.</summary>
    public void OpenGate()
    {
        if (!_controller.IsRunning)
        {
            return;
        }

        _gate.Open(_clock.UtcNow);
        GateTypedText = string.Empty;
        IsGateOpen = true;
        OnPropertyChanged(nameof(GateWaitText));
        ConfirmEndEarlyCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task EndEarlyAsync()
    {
        if (_settings.Current.StrictMode)
        {
            OpenGate();
        }
        else
        {
            await EndNowAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfirmEndEarly))]
    private async Task ConfirmEndEarlyAsync()
    {
        CloseGate();
        await EndNowAsync();
    }

    [RelayCommand]
    private void KeepFocusing() => CloseGate();

    private bool CanConfirmEndEarly() => _gate.IsSatisfied(_clock.UtcNow, GateTypedText);

    private async Task EndNowAsync()
    {
        try
        {
            await _controller.EndEarlyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ending the session failed");
        }
    }

    private void CloseGate()
    {
        _gate.Close();
        IsGateOpen = false;
        GateTypedText = string.Empty;
    }

    private void OnControllerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SessionController.State))
        {
            return;
        }

        if (!State.IsRunning && IsGateOpen)
        {
            CloseGate();
        }

        OnPropertyChanged(string.Empty);
        ConfirmEndEarlyCommand.NotifyCanExecuteChanged();
    }
}
