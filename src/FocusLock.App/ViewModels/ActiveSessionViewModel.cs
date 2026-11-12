using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLock.App.Services;
using FocusLock.Core.Abstractions;
using FocusLock.Core.Common;
using FocusLock.Core.Sessions;
using Microsoft.Extensions.Logging;

namespace FocusLock.App.ViewModels;

public sealed partial class ActiveSessionViewModel : ObservableObject
{
    private readonly SessionController _controller;
    private readonly IClock _clock;
    private readonly ILogger<ActiveSessionViewModel> _logger;

    public ActiveSessionViewModel(SessionController controller, IClock clock, ILogger<ActiveSessionViewModel> logger)
    {
        _controller = controller;
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

    /// <summary>False when the user committed to the timer; the session then runs to the end.</summary>
    public bool CanEndEarly => State.CanEndEarly;

    [RelayCommand]
    private async Task EndEarlyAsync()
    {
        if (!CanEndEarly)
        {
            return;
        }

        try
        {
            await _controller.EndEarlyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ending the session failed");
        }
    }

    private void OnControllerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SessionController.State))
        {
            OnPropertyChanged(string.Empty);
        }
    }
}
