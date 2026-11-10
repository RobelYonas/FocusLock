using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLock.App.Services;
using FocusLock.Core.Common;
using FocusLock.Core.Detection;
using FocusLock.Core.Settings;
using Microsoft.Extensions.Logging;

namespace FocusLock.App.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    public const int MinMinutes = 1;
    public const int MaxMinutes = 480;

    private readonly SessionController _controller;
    private readonly GameCatalog _catalog;
    private readonly SettingsService _settings;
    private readonly ILogger<HomeViewModel> _logger;
    private int _selectedMinutes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveMinutes), nameof(TimerText), nameof(TimerAccessibleText), nameof(ValidationText), nameof(PlanText), nameof(HasPlanText))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string _customMinutesText = "45";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveMinutes), nameof(TimerText), nameof(TimerAccessibleText), nameof(ValidationText), nameof(PlanText), nameof(HasPlanText))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private bool _isCustom;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isSearching = true;

    [ObservableProperty]
    private string _errorText = string.Empty;

    public HomeViewModel(
        SessionController controller,
        GameCatalog catalog,
        SettingsService settings,
        IUiDispatcher dispatcher,
        ILogger<HomeViewModel> logger)
    {
        _controller = controller;
        _catalog = catalog;
        _settings = settings;
        _logger = logger;

        var current = settings.Current;
        Options = current.PresetMinutes
            .Select(minutes => new LengthOption(minutes, $"{minutes} min", $"{minutes} minutes", OnOptionSelected))
            .Append(new LengthOption(null, "Custom", "Custom length", OnOptionSelected))
            .ToList();

        var initial = Options.FirstOrDefault(option => option.Minutes == current.DefaultMinutes) ?? Options[0];
        initial.IsSelected = true;

        catalog.Changed += (_, _) => dispatcher.Post(() =>
        {
            IsSearching = false;
            OnPropertyChanged(nameof(StatusText));
        });
        settings.Saved += (_, _) =>
        {
            OnPropertyChanged(nameof(PlanText));
            OnPropertyChanged(nameof(HasPlanText));
        };
        controller.PropertyChanged += OnControllerPropertyChanged;
    }

    public IReadOnlyList<LengthOption> Options { get; }

    /// <summary>The chosen length in minutes, or null while the custom entry is not a valid number.</summary>
    public int? EffectiveMinutes
    {
        get
        {
            if (!IsCustom)
            {
                return _selectedMinutes;
            }

            return int.TryParse(CustomMinutesText.Trim(), out var minutes) && minutes is >= MinMinutes and <= MaxMinutes ? minutes : null;
        }
    }

    public string TimerText => EffectiveMinutes is int minutes ? TimeText.Countdown(TimeSpan.FromMinutes(minutes)) : "00:00";

    public string TimerAccessibleText => EffectiveMinutes is int minutes
        ? $"Session length {TimeText.Spoken(TimeSpan.FromMinutes(minutes))}"
        : "Session length not set";

    public string ValidationText => IsCustom && EffectiveMinutes is null ? $"Enter a number of minutes from {MinMinutes} to {MaxMinutes}." : string.Empty;

    public string PlanText
    {
        get
        {
            var pomodoro = _settings.Current.Pomodoro;
            if (!pomodoro.Enabled || EffectiveMinutes is not int minutes)
            {
                return string.Empty;
            }

            var plan = _controller.BuildPlan(TimeSpan.FromMinutes(minutes));
            return $"{pomodoro.Cycles} focus blocks with {pomodoro.BreakMinutes} min breaks, about {TimeText.Duration(plan.TotalLength)} in total";
        }
    }

    public bool HasPlanText => PlanText.Length > 0;

    public string StatusText
    {
        get
        {
            if (IsSearching)
            {
                return "Looking for your games…";
            }

            return _catalog.BlockedGameCount switch
            {
                0 => "No games found yet. Add some on the Blocklist page.",
                1 => "1 game will be blocked",
                var count => $"{count} games will be blocked",
            };
        }
    }

    public string TodayText
    {
        get
        {
            var focused = _controller.FocusedToday();
            return focused >= TimeSpan.FromMinutes(1) ? $"Today: {TimeText.Duration(focused)} focused" : string.Empty;
        }
    }

    public bool HasSummary => _controller.LastSummary is not null;

    public string SummaryTitle => _controller.LastSummary?.EndedEarly == true ? "Session ended early" : "Nice work";

    public string SummaryBody
    {
        get
        {
            if (_controller.LastSummary is not { } summary)
            {
                return string.Empty;
            }

            var blocked = summary.BlockedAttempts switch
            {
                0 => "No game launches were blocked.",
                1 => "1 game launch was blocked.",
                var count => $"{count} game launches were blocked.",
            };

            return $"You focused for {TimeText.Spoken(summary.FocusedTime)}. {blocked}";
        }
    }

    [RelayCommand]
    private void DismissSummary() => _controller.DismissSummary();

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (EffectiveMinutes is not int minutes)
        {
            return;
        }

        ErrorText = string.Empty;
        _settings.Update(settings => settings.DefaultMinutes = minutes);
        try
        {
            await _controller.StartAsync(TimeSpan.FromMinutes(minutes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Starting a session failed");
            ErrorText = "The session could not start. The log file has the details.";
        }
    }

    private bool CanStart() => EffectiveMinutes is not null && !_controller.IsRunning;

    private void OnOptionSelected(LengthOption option)
    {
        if (option.Minutes is int minutes)
        {
            _selectedMinutes = minutes;
        }

        IsCustom = option.Minutes is null;
        OnPropertyChanged(nameof(EffectiveMinutes));
        OnPropertyChanged(nameof(TimerText));
        OnPropertyChanged(nameof(TimerAccessibleText));
        OnPropertyChanged(nameof(PlanText));
        OnPropertyChanged(nameof(HasPlanText));
        StartCommand.NotifyCanExecuteChanged();
    }

    private void OnControllerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SessionController.LastSummary):
                OnPropertyChanged(nameof(HasSummary));
                OnPropertyChanged(nameof(SummaryTitle));
                OnPropertyChanged(nameof(SummaryBody));
                OnPropertyChanged(nameof(TodayText));
                break;
            case nameof(SessionController.State):
                StartCommand.NotifyCanExecuteChanged();
                break;
        }
    }
}
