using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeatMapTracker.Core.Data;
using HeatMapTracker.Core.Models;
using HeatMapTracker.Core.Stats;
using HeatMapTracker.Services;

namespace HeatMapTracker.ViewModels;

/// <summary>
/// State for <see cref="Views.MainView"/>: the saved location history, tracking status and summary stats.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ILocationRepository _repository;
    private readonly ILocationTracker _tracker;
    private bool _historyLoaded;

    /// <summary>Creates the view model.</summary>
    public MainViewModel(ILocationRepository repository, ILocationTracker tracker)
    {
        _repository = repository;
        _tracker = tracker;
        _tracker.PointRecorded += OnPointRecorded;
        _tracker.TrackingFailed += OnTrackingFailed;
    }

    /// <summary>Raised when the user asks to start tracking but it could not start (e.g. permission denied).</summary>
    public event EventHandler<TrackingStartResult>? TrackingStartFailed;

    /// <summary>Every saved reading, oldest first. Replaced wholesale when history is loaded.</summary>
    [ObservableProperty]
    public partial ObservableCollection<LocationPoint> Points { get; set; } = [];

    /// <summary>The most recent reading, used to center the map.</summary>
    [ObservableProperty]
    public partial LocationPoint? LatestPoint { get; set; }

    /// <summary>Whether location updates are currently being recorded.</summary>
    [ObservableProperty]
    public partial bool IsTracking { get; set; }

    /// <summary>Headline on the stats card, e.g. "Tracking" or "Paused".</summary>
    [ObservableProperty]
    public partial string StatusText { get; set; } = "Starting…";

    /// <summary>Number of saved readings, formatted for display.</summary>
    [ObservableProperty]
    public partial string PointCountText { get; set; } = "0 points";

    /// <summary>Total distance travelled, e.g. "12.4 km".</summary>
    [ObservableProperty]
    public partial string DistanceText { get; set; } = "0 m";

    /// <summary>Total time tracked, e.g. "18 min".</summary>
    [ObservableProperty]
    public partial string TrackedTimeText { get; set; } = "0 s";

    /// <summary>Longest time spent in one place, e.g. "3 min".</summary>
    [ObservableProperty]
    public partial string LongestStopText { get; set; } = "0 s";

    /// <summary>
    /// Loads saved history on first call (so the map survives app restarts), then starts tracking.
    /// </summary>
    public async Task<TrackingStartResult> StartAsync()
    {
        if (!_historyLoaded)
        {
            await _repository.InitializeAsync();
            var saved = await _repository.GetAllAsync();
            Points = new ObservableCollection<LocationPoint>(saved);
            LatestPoint = saved.Count > 0 ? saved[^1] : null;
            _historyLoaded = true;
            RefreshStats();
        }

        var result = await _tracker.StartAsync();
        IsTracking = _tracker.IsTracking;
        StatusText = result switch
        {
            TrackingStartResult.Started => "Tracking",
            TrackingStartResult.PermissionDenied => "Location permission denied",
            TrackingStartResult.LocationDisabled => "Location is turned off",
            _ => "Location not supported",
        };
        return result;
    }

    /// <summary>Stops tracking, e.g. when the page is hidden or the user pauses.</summary>
    public void Stop()
    {
        _tracker.Stop();
        IsTracking = false;
        StatusText = "Paused";
    }

    /// <summary>Pauses tracking if it is running, otherwise starts it.</summary>
    [RelayCommand]
    private async Task ToggleTrackingAsync()
    {
        if (IsTracking)
        {
            Stop();
            return;
        }

        var result = await StartAsync();
        if (result != TrackingStartResult.Started)
        {
            TrackingStartFailed?.Invoke(this, result);
        }
    }

    /// <summary>Deletes all saved readings and clears the map. The view confirms with the user first.</summary>
    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        await _repository.ClearAsync();
        Points.Clear();
        LatestPoint = null;
        RefreshStats();
    }

    private void OnPointRecorded(object? sender, LocationPoint point)
    {
        // Tracker events arrive on a background thread; bound collections must change on the UI thread.
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Points.Add(point);
            LatestPoint = point;
            RefreshStats();
        });
    }

    private void OnTrackingFailed(object? sender, string message) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsTracking = _tracker.IsTracking;
            StatusText = message;
        });

    /// <summary>Recomputes the stats card. Linear in the number of points, which is fine for a day of history.</summary>
    private void RefreshStats()
    {
        var stats = TrackingStats.Calculate(Points);
        PointCountText = Points.Count == 1 ? "1 point" : $"{Points.Count:N0} points";
        DistanceText = TrackingStats.FormatDistance(stats.DistanceMeters);
        TrackedTimeText = TrackingStats.FormatDuration(stats.TrackedTime);
        LongestStopText = TrackingStats.FormatDuration(stats.LongestStop);
    }
}
