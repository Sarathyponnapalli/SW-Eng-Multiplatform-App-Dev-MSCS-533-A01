using HeatMapTracker.Core.Models;

namespace HeatMapTracker.Services;

/// <summary>Outcome of trying to start location tracking.</summary>
public enum TrackingStartResult
{
    /// <summary>Tracking is running.</summary>
    Started,

    /// <summary>The user denied the location permission.</summary>
    PermissionDenied,

    /// <summary>Location services are turned off on the device.</summary>
    LocationDisabled,

    /// <summary>The device has no location support.</summary>
    NotSupported,
}

/// <summary>
/// Samples the device location while the app is in the foreground and saves each reading.
/// </summary>
public interface ILocationTracker
{
    /// <summary>Raised (on a background thread) after a reading has been saved.</summary>
    event EventHandler<LocationPoint>? PointRecorded;

    /// <summary>Raised when tracking stops unexpectedly, with a user-readable reason.</summary>
    event EventHandler<string>? TrackingFailed;

    /// <summary>Whether location updates are currently being received.</summary>
    bool IsTracking { get; }

    /// <summary>Requests permission if needed and starts listening for location updates.</summary>
    Task<TrackingStartResult> StartAsync();

    /// <summary>Stops listening for location updates. Safe to call when not tracking.</summary>
    void Stop();
}
