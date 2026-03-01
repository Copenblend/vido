using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vido.Core.Logging;

namespace Vido.ViewModels;

/// <summary>
/// ViewModel for the Output Log panel. Observes <see cref="ILogService"/> and
/// exposes a filtered, formatted collection of log entries for UI display.
/// Supports level filtering and auto-scroll behavior.
/// </summary>
public partial class OutputLogViewModel : ObservableObject, IDisposable
{
    private readonly ILogService _logService;
    private readonly SynchronizationContext? _syncContext;
    private LogLevel _minimumLevel = LogLevel.Debug;

    /// <summary>
    /// Filtered log entries currently displayed in the panel.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<LogEntryViewModel> _entries = [];

    /// <summary>
    /// Whether auto-scroll to the latest entry is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isAutoScrollEnabled = true;

    /// <summary>
    /// Whether the log has any entries (for empty state display).
    /// </summary>
    [ObservableProperty]
    private bool _hasEntries;

    /// <summary>
    /// Text summarizing the active filter (e.g. "All" or "Warnings+").
    /// </summary>
    [ObservableProperty]
    private string _filterText = "All";
    
    /// <summary>
    /// Creates the output log view model, loading existing entries and subscribing to new log events.
    /// </summary>
    /// <param name="logService">Log service to observe for new entries and retrieve existing ones.</param>
    public OutputLogViewModel(ILogService logService)
    {
        _logService = logService;
        _syncContext = SynchronizationContext.Current;

        // Load any existing entries
        foreach (var entry in _logService.Entries)
        {
            if (entry.Level >= _minimumLevel)
                Entries.Add(new LogEntryViewModel(entry));
        }

        HasEntries = Entries.Count > 0;

        // Subscribe to new entries
        _logService.EntryAdded += OnEntryAdded;
    }

    private void OnEntryAdded(LogEntry entry)
    {
        if (entry.Level < _minimumLevel) return;

        // Marshal to UI thread if a synchronization context was captured at construction
        if (_syncContext is not null)
        {
            _syncContext.Post(_ => AddEntry(entry), null);
        }
        else
        {
            AddEntry(entry);
        }
    }

    private void AddEntry(LogEntry entry)
    {
        Entries.Add(new LogEntryViewModel(entry));
        HasEntries = true;
    }

    /// <summary>
    /// Clears all log entries.
    /// </summary>
    [RelayCommand]
    public void ClearLog()
    {
        _logService.Clear();
        Entries = [];
        HasEntries = false;
    }

    /// <summary>
    /// Toggles auto-scroll on/off.
    /// </summary>
    [RelayCommand]
    public void ToggleAutoScroll()
    {
        IsAutoScrollEnabled = !IsAutoScrollEnabled;
    }

    /// <summary>
    /// Cycles through log level filters: All → Info+ → Warnings+ → Errors → All.
    /// </summary>
    [RelayCommand]
    public void CycleFilter()
    {
        var nextLevel = _minimumLevel switch
        {
            LogLevel.Debug => LogLevel.Info,
            LogLevel.Info => LogLevel.Warning,
            LogLevel.Warning => LogLevel.Error,
            _ => LogLevel.Debug
        };

        SetFilter(nextLevel);
    }

    /// <summary>
    /// Sets the filter to a specific level.
    /// </summary>
    /// <param name="level">Minimum log level to display.</param>
    public void SetFilter(LogLevel level)
    {
        _minimumLevel = level;
        FilterText = LevelToFilterText(level);
        RebuildFilteredEntries();
    }

    private static string LevelToFilterText(LogLevel level) => level switch
    {
        LogLevel.Debug => "All",
        LogLevel.Info => "Info+",
        LogLevel.Warning => "Warn+",
        LogLevel.Error => "Errors",
        _ => "All"
    };

    private void RebuildFilteredEntries()
    {
        Entries = new ObservableCollection<LogEntryViewModel>(
            _logService.Entries
                .Where(entry => entry.Level >= _minimumLevel)
                .Select(entry => new LogEntryViewModel(entry)));
        HasEntries = Entries.Count > 0;
    }

    /// <summary>
    /// Unsubscribes from the log service to prevent memory leaks.
    /// </summary>
    public void Dispose()
    {
        _logService.EntryAdded -= OnEntryAdded;
    }
}
