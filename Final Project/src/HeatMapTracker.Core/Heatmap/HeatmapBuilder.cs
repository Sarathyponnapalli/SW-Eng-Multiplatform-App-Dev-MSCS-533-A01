using HeatMapTracker.Core.Models;

namespace HeatMapTracker.Core.Heatmap;

/// <summary>
/// Turns raw location readings into weighted <see cref="HeatCell"/>s, the same idea used by
/// weighted heat-map layers: nearby readings are binned into a grid, and each cell's weight
/// is how many readings it holds. Because readings are sampled at a fixed time interval,
/// the count is proportional to the time spent in that area.
/// </summary>
public static class HeatmapBuilder
{
    /// <summary>Approximate length of one degree of latitude, in meters.</summary>
    private const double MetersPerDegreeLatitude = 111_320d;

    /// <summary>Lower bound for cos(latitude) so cells near the poles stay finite.</summary>
    private const double MinLongitudeScale = 0.01;

    /// <summary>Default grid cell edge length in meters.</summary>
    public const double DefaultCellSizeMeters = 25d;

    /// <summary>
    /// Aggregates <paramref name="points"/> into grid cells about <paramref name="cellSizeMeters"/> wide.
    /// </summary>
    /// <param name="points">Readings to aggregate. Null or empty input yields no cells.</param>
    /// <param name="cellSizeMeters">Approximate edge length of each square grid cell.</param>
    /// <returns>
    /// One cell per occupied grid square. Intensity is log-scaled,
    /// <c>log(1 + count) / log(1 + maxCount)</c>, so one long stop does not wash out the rest
    /// of the route. The busiest cell always has intensity 1.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cellSizeMeters"/> is not positive.</exception>
    /// <remarks>Routes that cross the 180th meridian are not merged across it; that is fine for this app.</remarks>
    public static IReadOnlyList<HeatCell> Build(IEnumerable<LocationPoint>? points, double cellSizeMeters = DefaultCellSizeMeters)
    {
        if (double.IsNaN(cellSizeMeters) || cellSizeMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSizeMeters), cellSizeMeters, "Cell size must be greater than zero.");
        }

        if (points is null)
        {
            return [];
        }

        var cellHeightDegrees = cellSizeMeters / MetersPerDegreeLatitude;
        var accumulators = new Dictionary<(long Row, long Column), CellAccumulator>();

        foreach (var point in points)
        {
            var key = GetCellKey(point.Latitude, point.Longitude, cellHeightDegrees);
            if (!accumulators.TryGetValue(key, out var accumulator))
            {
                accumulator = new CellAccumulator();
                accumulators[key] = accumulator;
            }

            accumulator.Add(point.Latitude, point.Longitude);
        }

        if (accumulators.Count == 0)
        {
            return [];
        }

        var maxCount = accumulators.Values.Max(a => a.Count);
        var logMax = Math.Log(1 + maxCount);

        return accumulators.Values
            .Select(a => new HeatCell(
                a.LatitudeSum / a.Count,
                a.LongitudeSum / a.Count,
                a.Count,
                Math.Log(1 + a.Count) / logMax))
            .ToList();
    }

    /// <summary>
    /// Maps a coordinate to its grid cell. Cell width in degrees of longitude grows with latitude
    /// (scaled by 1 / cos(latitude)) so cells stay roughly square on the ground. The scale is taken
    /// from the row's center latitude so every point in a row uses the same column boundaries.
    /// </summary>
    private static (long Row, long Column) GetCellKey(double latitude, double longitude, double cellHeightDegrees)
    {
        var row = (long)Math.Floor(latitude / cellHeightDegrees);
        var rowCenterLatitude = (row + 0.5) * cellHeightDegrees;
        var longitudeScale = Math.Max(Math.Cos(rowCenterLatitude * Math.PI / 180d), MinLongitudeScale);
        var cellWidthDegrees = cellHeightDegrees / longitudeScale;
        var column = (long)Math.Floor(longitude / cellWidthDegrees);
        return (row, column);
    }

    /// <summary>Running totals for one cell, used to compute its count and centroid.</summary>
    private sealed class CellAccumulator
    {
        public int Count { get; private set; }

        public double LatitudeSum { get; private set; }

        public double LongitudeSum { get; private set; }

        public void Add(double latitude, double longitude)
        {
            Count++;
            LatitudeSum += latitude;
            LongitudeSum += longitude;
        }
    }
}
