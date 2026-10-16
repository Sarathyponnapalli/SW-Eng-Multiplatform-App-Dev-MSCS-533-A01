using System.ComponentModel;
using HeatMapTracker.Core.Models;
using HeatMapTracker.Services;
using HeatMapTracker.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Maps;

namespace HeatMapTracker.Views;

/// <summary>
/// The main (and only) page: shows the user's location history as a heat map.
/// Tracking runs while the page is visible and pauses when it is hidden.
/// Code-behind handles only view concerns: dialogs, camera moves and animation.
/// </summary>
public partial class MainView : ContentPage
{
    private const string PulseAnimationName = "LivePulse";
    private static readonly Distance InitialZoomRadius = Distance.FromKilometers(1);

    /// <summary>Smallest span used when fitting the history, so a single point is not zoomed in absurdly far.</summary>
    private const double MinFitSpanDegrees = 0.01;

    private readonly MainViewModel _viewModel;
    private readonly ILogger<MainView> _logger;
    private bool _hasCenteredMap;

    /// <summary>Creates the page with its view model (supplied by dependency injection).</summary>
    public MainView(MainViewModel viewModel, ILogger<MainView> logger)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _logger = logger;
        BindingContext = viewModel;
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.TrackingStartFailed += OnTrackingStartFailed;

        try
        {
            var result = await _viewModel.StartAsync();
            CenterOn(_viewModel.LatestPoint);
            UpdatePulse();

            if (result != TrackingStartResult.Started)
            {
                await ShowStartProblemAsync(result);
            }
        }
        catch (Exception ex)
        {
            // async void lifecycle method: never let an exception escape and crash the app.
            _logger.LogError(ex, "Failed to start tracking.");
            await DisplayAlertAsync("Something went wrong", "Tracking could not start. Please try again.", "OK");
        }
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.TrackingStartFailed -= OnTrackingStartFailed;
        _viewModel.Stop();
        UpdatePulse();
        base.OnDisappearing();
    }

    /// <summary>Asks for confirmation before deleting history, since it cannot be undone.</summary>
    private async void OnClearClicked(object? sender, EventArgs e)
    {
        try
        {
            var confirmed = await DisplayAlertAsync(
                "Delete all history?",
                $"This permanently removes {_viewModel.PointCountText} from this device.",
                "Delete",
                "Cancel");

            if (confirmed)
            {
                await _viewModel.ClearHistoryCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear history.");
            await DisplayAlertAsync("Something went wrong", "History could not be deleted.", "OK");
        }
    }

    /// <summary>Zooms the map out to show every recorded point.</summary>
    private void OnFitHistoryClicked(object? sender, EventArgs e)
    {
        var points = _viewModel.Points;
        if (points.Count == 0)
        {
            return;
        }

        var minLat = points.Min(p => p.Latitude);
        var maxLat = points.Max(p => p.Latitude);
        var minLon = points.Min(p => p.Longitude);
        var maxLon = points.Max(p => p.Longitude);

        var center = new Location((minLat + maxLat) / 2, (minLon + maxLon) / 2);
        var latSpan = Math.Max((maxLat - minLat) * 1.3, MinFitSpanDegrees);
        var lonSpan = Math.Max((maxLon - minLon) * 1.3, MinFitSpanDegrees);
        TrackingMap.MoveToRegion(new MapSpan(center, latSpan, lonSpan));
    }

    private async void OnTrackingStartFailed(object? sender, TrackingStartResult result)
    {
        try
        {
            await ShowStartProblemAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show tracking error.");
        }
    }

    private Task ShowStartProblemAsync(TrackingStartResult result) => result switch
    {
        TrackingStartResult.PermissionDenied => DisplayAlertAsync(
            "Location needed",
            "Heat Map Tracker records where you go to draw your heat map. Allow location access in Settings to start tracking.",
            "OK"),
        TrackingStartResult.LocationDisabled => DisplayAlertAsync(
            "Location is off", "Turn on location services to start tracking.", "OK"),
        TrackingStartResult.NotSupported => DisplayAlertAsync(
            "Not supported", "This device cannot provide its location.", "OK"),
        _ => Task.CompletedTask,
    };

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            // Center on the first known position, then leave the camera to the user.
            case nameof(MainViewModel.LatestPoint) when !_hasCenteredMap:
                CenterOn(_viewModel.LatestPoint);
                break;
            case nameof(MainViewModel.IsTracking):
                UpdatePulse();
                break;
        }
    }

    private void CenterOn(LocationPoint? point)
    {
        if (point is null)
        {
            return;
        }

        TrackingMap.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(point.Latitude, point.Longitude), InitialZoomRadius));
        _hasCenteredMap = true;
    }

    /// <summary>Pulses the live dot while tracking, so it is obvious the app is recording.</summary>
    private void UpdatePulse()
    {
        this.AbortAnimation(PulseAnimationName);
        LiveDot.Opacity = 1;

        if (_viewModel.IsTracking)
        {
            var pulse = new Animation(v => LiveDot.Opacity = v, 1, 0.25);
            pulse.Commit(this, PulseAnimationName, length: 900, easing: Easing.SinInOut,
                finished: (_, _) => LiveDot.Opacity = 1, repeat: () => _viewModel.IsTracking);
        }
    }
}
