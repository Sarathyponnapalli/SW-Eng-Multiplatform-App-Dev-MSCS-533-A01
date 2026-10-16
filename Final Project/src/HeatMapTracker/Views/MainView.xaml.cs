using System.ComponentModel;
using HeatMapTracker.Core.Models;
using HeatMapTracker.Services;
using HeatMapTracker.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Maps;

namespace HeatMapTracker.Views;

/// <summary>
/// The main (and only) page: shows the user's location history on a map.
/// Tracking runs while the page is visible and pauses when it is hidden.
/// </summary>
public partial class MainView : ContentPage
{
    private static readonly Distance InitialZoomRadius = Distance.FromKilometers(1);

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

        try
        {
            var result = await _viewModel.StartAsync();
            CenterOn(_viewModel.LatestPoint);

            if (result == TrackingStartResult.PermissionDenied)
            {
                await DisplayAlertAsync(
                    "Location needed",
                    "Heat Map Tracker records where you go to draw your heat map. Allow location access in Settings to start tracking.",
                    "OK");
            }
            else if (result == TrackingStartResult.LocationDisabled)
            {
                await DisplayAlertAsync("Location is off", "Turn on location services to start tracking.", "OK");
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
        _viewModel.Stop();
        base.OnDisappearing();
    }

    /// <summary>Centers the map on the first known position, then leaves the camera to the user.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.LatestPoint) && !_hasCenteredMap)
        {
            CenterOn(_viewModel.LatestPoint);
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
}
