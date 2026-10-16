using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeatMapTracker.Core.Data;
using HeatMapTracker.Core.Models;
using HeatMapTracker.Services;

namespace HeatMapTracker.ViewModels;

/// <summary>
/// State for <see cref="Views.MainView"/>: the saved location history and tracking status.
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

    /// <summary>Every saved reading, oldest first. Replaced wholesale when history is loaded.</summary>
    [ObservableProperty]
    public partial ObservableCollection<LocationPoint> Points { get; set; } = [];

    /// <summary>The most recent reading, used to center the map.</summary>
    [ObservableProperty]
    public partial LocationPoint? LatestPoint { get; set; }

    /// <summary>Short status line shown over the map.</summary>
    [ObservableProperty]
    public partial string StatusText { get; set; } = "Starting…";

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
        }

        var result = await _tracker.StartAsync();
        StatusText = result switch
        {
            TrackingStartResult.Started => FormatTrackingStatus(),
            TrackingStartResult.PermissionDenied => "Location permission denied",
            TrackingStartResult.LocationDisabled => "Location is turned off",
            _ => "Location not supported on this device",
        };
        return result;
    }

    /// <summary>Stops tracking, e.g. when the page is hidden.</summary>
    public void Stop()
    {
        _tracker.Stop();
        StatusText = $"Paused · {Points.Count} points";
    }

    /// <summary>Deletes all saved readings and clears the map.</summary>
    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        await _repository.ClearAsync();
        Points.Clear();
        LatestPoint = null;
        StatusText = FormatTrackingStatus();
    }

    private void OnPointRecorded(object? sender, LocationPoint point)
    {
        // Tracker events arrive on a background thread; bound collections must change on the UI thread.
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Points.Add(point);
            LatestPoint = point;
            StatusText = FormatTrackingStatus();
        });
    }

    private void OnTrackingFailed(object? sender, string message) =>
        MainThread.BeginInvokeOnMainThread(() => StatusText = message);

    private string FormatTrackingStatus() =>
        _tracker.IsTracking ? $"Tracking · {Points.Count} points" : $"{Points.Count} points";
}
