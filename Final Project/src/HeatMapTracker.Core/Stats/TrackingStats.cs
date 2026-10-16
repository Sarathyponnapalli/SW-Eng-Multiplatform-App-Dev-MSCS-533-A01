using HeatMapTracker.Core.Models;

namespace HeatMapTracker.Core.Stats;

/// <summary>
/// Summary figures for a location history, shown on the app's stats card.
/// </summary>
/// <param name="DistanceMeters">Total distance travelled along the recorded path.</param>
/// <param name="TrackedTime">Total time covered by tracking sessions.</param>
/// <param name="LongestStop">Longest continuous time spent within <see cref="StopRadiusMeters"/> of one place.</param>
public sealed record TrackingStats(double DistanceMeters, TimeSpan TrackedTime, TimeSpan LongestStop)
{
    /// <summary>A gap longer than this between readings starts a new session (the app was closed).</summary>
    public static readonly TimeSpan SessionGap = TimeSpan.FromMinutes(5);

    /// <summary>Movement smaller than this is treated as GPS jitter, not travel.</summary>
    public const double JitterMeters = 3;

    /// <summary>
    /// Steps faster than this (about 1,200 km/h, faster than an airliner) are treated as a GPS jump
    /// or teleport, not travel, and start a new session. Deliberately generous so fast route
    /// playback in the emulator still counts as travel.
    /// </summary>
    public const double MaxPlausibleSpeedMetersPerSecond = 340;

    /// <summary>Readings within this distance of a stop's first reading count as the same stop.</summary>
    public const double StopRadiusMeters = 30;

    private const double EarthRadiusMeters = 6_371_000;

    /// <summary>Statistics for an empty history.</summary>
    public static TrackingStats Empty { get; } = new(0, TimeSpan.Zero, TimeSpan.Zero);

    /// <summary>
    /// Computes statistics for <paramref name="points"/>, which must be ordered oldest first.
    /// Segments that span a <see cref="SessionGap"/> are ignored, so closing the app in one city and
    /// reopening it in another does not count as travel.
    /// </summary>
    public static TrackingStats Calculate(IReadOnlyList<LocationPoint>? points)
    {
        if (points is null || points.Count == 0)
        {
            return Empty;
        }

        var distance = 0d;
        var tracked = TimeSpan.Zero;
        var longestStop = TimeSpan.Zero;
        var stopAnchor = points[0];

        for (var i = 1; i < points.Count; i++)
        {
            var previous = points[i - 1];
            var current = points[i];
            var gap = current.TimestampUtc - previous.TimestampUtc;
            var step = HaversineMeters(previous, current);
            var sameSession = gap >= TimeSpan.Zero && gap <= SessionGap && IsPlausibleMove(step, gap);

            if (sameSession)
            {
                tracked += gap;
                if (step >= JitterMeters)
                {
                    distance += step;
                }
            }

            // A stop continues while readings stay near its first reading within one session.
            if (sameSession && HaversineMeters(stopAnchor, current) <= StopRadiusMeters)
            {
                var stopLength = current.TimestampUtc - stopAnchor.TimestampUtc;
                if (stopLength > longestStop)
                {
                    longestStop = stopLength;
                }
            }
            else
            {
                stopAnchor = current;
            }
        }

        return new TrackingStats(distance, tracked, longestStop);
    }

    /// <summary>
    /// Whether moving <paramref name="meters"/> in <paramref name="elapsed"/> is physically plausible.
    /// Readings taken in the same instant are allowed only if they are close together.
    /// </summary>
    private static bool IsPlausibleMove(double meters, TimeSpan elapsed) =>
        elapsed.TotalSeconds > 0
            ? meters / elapsed.TotalSeconds <= MaxPlausibleSpeedMetersPerSecond
            : meters <= StopRadiusMeters;

    /// <summary>Great-circle distance between two readings, in meters.</summary>
    public static double HaversineMeters(LocationPoint a, LocationPoint b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        static double ToRadians(double degrees) => degrees * Math.PI / 180d;

        var dLat = ToRadians(b.Latitude - a.Latitude);
        var dLon = ToRadians(b.Longitude - a.Longitude);
        var h = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
            + (Math.Cos(ToRadians(a.Latitude)) * Math.Cos(ToRadians(b.Latitude)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    /// <summary>Formats a distance for display, e.g. "850 m" or "12.4 km".</summary>
    public static string FormatDistance(double meters) =>
        meters < 1000 ? $"{meters:F0} m" : $"{meters / 1000:F1} km";

    /// <summary>Formats a duration for display, e.g. "45 s", "18 min" or "1 h 05 min".</summary>
    public static string FormatDuration(TimeSpan duration) => duration switch
    {
        { TotalMinutes: < 1 } => $"{(int)duration.TotalSeconds} s",
        { TotalHours: < 1 } => $"{(int)duration.TotalMinutes} min",
        _ => $"{(int)duration.TotalHours} h {duration.Minutes:D2} min",
    };
}
