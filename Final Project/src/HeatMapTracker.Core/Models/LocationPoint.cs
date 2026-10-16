using SQLite;

namespace HeatMapTracker.Core.Models;

/// <summary>
/// A single location reading captured from the device, persisted as one row in SQLite.
/// </summary>
[Table("location_points")]
public sealed class LocationPoint
{
    /// <summary>Auto-incremented primary key assigned by SQLite.</summary>
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Latitude in decimal degrees, from -90 to 90.</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude in decimal degrees, from -180 to 180.</summary>
    public double Longitude { get; set; }

    /// <summary>Horizontal accuracy reported by the device, in meters, if known.</summary>
    public double? AccuracyMeters { get; set; }

    /// <summary>When the reading was taken, always stored in UTC.</summary>
    [Indexed]
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Creates a validated point. Prefer this over the parameterless constructor,
    /// which exists only so SQLite can materialize rows.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Latitude or longitude is out of range or not a number.</exception>
    public static LocationPoint Create(double latitude, double longitude, DateTime timestampUtc, double? accuracyMeters = null)
    {
        var point = new LocationPoint
        {
            Latitude = latitude,
            Longitude = longitude,
            AccuracyMeters = accuracyMeters,
            TimestampUtc = timestampUtc.ToUniversalTime(),
        };
        point.Validate();
        return point;
    }

    /// <summary>Throws if the coordinates are not a valid position on Earth.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Latitude or longitude is out of range or not a number.</exception>
    public void Validate()
    {
        if (double.IsNaN(Latitude) || Latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(Latitude), Latitude, "Latitude must be between -90 and 90 degrees.");
        }

        if (double.IsNaN(Longitude) || Longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(Longitude), Longitude, "Longitude must be between -180 and 180 degrees.");
        }
    }
}
