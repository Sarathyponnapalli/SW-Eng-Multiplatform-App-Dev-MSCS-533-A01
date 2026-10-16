using HeatMapTracker.Core.Data;
using HeatMapTracker.Core.Models;
using Microsoft.Extensions.Logging;

namespace HeatMapTracker.Services;

/// <summary>
/// Foreground location tracker built on .NET MAUI <see cref="IGeolocation"/>.
/// Readings arrive about every <see cref="SampleInterval"/>; each accurate reading is saved to the
/// repository. Sampling by time (not by distance moved) means places where the user stays longer
/// collect more readings, which is what makes them "hotter" on the heat map.
/// </summary>
public sealed class LocationTracker : ILocationTracker
{
    /// <summary>Target time between saved readings.</summary>
    public static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(5);

    /// <summary>Readings less accurate than this are discarded as noise.</summary>
    public const double MaxAccuracyMeters = 50;

    // Platforms may deliver updates faster than requested; enforce the interval with a little slack.
    private static readonly TimeSpan MinTimeBetweenSaves = SampleInterval - TimeSpan.FromMilliseconds(500);

    private readonly IGeolocation _geolocation;
    private readonly ILocationRepository _repository;
    private readonly ILogger<LocationTracker> _logger;
    private DateTimeOffset _lastSavedAt = DateTimeOffset.MinValue;

    /// <summary>Creates the tracker.</summary>
    public LocationTracker(IGeolocation geolocation, ILocationRepository repository, ILogger<LocationTracker> logger)
    {
        _geolocation = geolocation;
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public event EventHandler<LocationPoint>? PointRecorded;

    /// <inheritdoc />
    public event EventHandler<string>? TrackingFailed;

    /// <inheritdoc />
    public bool IsTracking => _geolocation.IsListeningForeground;

    /// <inheritdoc />
    public async Task<TrackingStartResult> StartAsync()
    {
        if (IsTracking)
        {
            return TrackingStartResult.Started;
        }

        // Permission prompts must be shown from the UI thread.
        var status = await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var current = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            return current == PermissionStatus.Granted
                ? current
                : await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        });

        if (status != PermissionStatus.Granted)
        {
            _logger.LogWarning("Location permission not granted: {Status}", status);
            return TrackingStartResult.PermissionDenied;
        }

        try
        {
            _geolocation.LocationChanged += OnLocationChanged;
            _geolocation.ListeningFailed += OnListeningFailed;

            var request = new GeolocationListeningRequest(GeolocationAccuracy.High, SampleInterval);
            var started = await _geolocation.StartListeningForegroundAsync(request);
            if (!started)
            {
                Unsubscribe();
                return TrackingStartResult.LocationDisabled;
            }

            _logger.LogInformation("Location tracking started (interval {Interval}).", SampleInterval);
            return TrackingStartResult.Started;
        }
        catch (FeatureNotEnabledException ex)
        {
            Unsubscribe();
            _logger.LogWarning(ex, "Location services are disabled.");
            return TrackingStartResult.LocationDisabled;
        }
        catch (FeatureNotSupportedException ex)
        {
            Unsubscribe();
            _logger.LogWarning(ex, "Location is not supported on this device.");
            return TrackingStartResult.NotSupported;
        }
        catch (PermissionException ex)
        {
            Unsubscribe();
            _logger.LogWarning(ex, "Location permission was revoked.");
            return TrackingStartResult.PermissionDenied;
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        Unsubscribe();
        if (_geolocation.IsListeningForeground)
        {
            _geolocation.StopListeningForeground();
            _logger.LogInformation("Location tracking stopped.");
        }
    }

    /// <summary>
    /// Saves an accurate, sufficiently spaced reading and notifies listeners.
    /// This is an event handler, so it is async void; every failure is caught and reported.
    /// </summary>
    private async void OnLocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
    {
        try
        {
            var location = e.Location;
            if (location.Accuracy is > MaxAccuracyMeters)
            {
                _logger.LogDebug("Skipped inaccurate reading ({Accuracy:F0} m).", location.Accuracy);
                return;
            }

            var now = DateTimeOffset.UtcNow;
            if (now - _lastSavedAt < MinTimeBetweenSaves)
            {
                return;
            }

            _lastSavedAt = now;
            var point = LocationPoint.Create(location.Latitude, location.Longitude, location.Timestamp.UtcDateTime, location.Accuracy);
            await _repository.AddAsync(point);
            PointRecorded?.Invoke(this, point);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save a location reading.");
            TrackingFailed?.Invoke(this, "Could not save your location. Tracking will keep trying.");
        }
    }

    private void OnListeningFailed(object? sender, GeolocationListeningFailedEventArgs e)
    {
        _logger.LogWarning("Location listening failed: {Error}", e.Error);
        Unsubscribe();
        TrackingFailed?.Invoke(this, $"Location updates stopped ({e.Error}).");
    }

    private void Unsubscribe()
    {
        _geolocation.LocationChanged -= OnLocationChanged;
        _geolocation.ListeningFailed -= OnListeningFailed;
    }
}
