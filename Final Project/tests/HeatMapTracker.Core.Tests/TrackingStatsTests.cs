using HeatMapTracker.Core.Models;
using HeatMapTracker.Core.Stats;

namespace HeatMapTracker.Core.Tests;

/// <summary>
/// Tests for <see cref="TrackingStats"/>.
/// </summary>
public sealed class TrackingStatsTests
{
    private const double MetersPerDegreeLatitude = 111_195d; // matches the haversine Earth radius
    private static readonly DateTime BaseTime = new(2026, 10, 15, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Calculate_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(TrackingStats.Empty, TrackingStats.Calculate(null));
        Assert.Equal(TrackingStats.Empty, TrackingStats.Calculate([]));
    }

    [Fact]
    public void Calculate_StraightWalk_SumsDistanceAndTime()
    {
        // 11 readings, 100 m apart, 5 s apart: 1 km in 50 s.
        var points = Enumerable.Range(0, 11).Select(i => Point(i * 100, i * 5)).ToList();

        var stats = TrackingStats.Calculate(points);

        Assert.Equal(1000, stats.DistanceMeters, tolerance: 1);
        Assert.Equal(TimeSpan.FromSeconds(50), stats.TrackedTime);
        Assert.Equal(TimeSpan.Zero, stats.LongestStop);
    }

    [Fact]
    public void Calculate_GpsJitter_IsNotCountedAsDistance()
    {
        var points = Enumerable.Range(0, 10).Select(i => Point(i % 2, i * 5)).ToList(); // 1 m wobble

        Assert.Equal(0, TrackingStats.Calculate(points).DistanceMeters);
    }

    [Fact]
    public void Calculate_SessionGap_IsNotCountedAsTravelOrTime()
    {
        var points = new[]
        {
            Point(0, 0),
            Point(100, 5),
            Point(50_000, 3600), // reopened an hour later, 50 km away
            Point(50_100, 3605),
        };

        var stats = TrackingStats.Calculate(points);

        Assert.Equal(200, stats.DistanceMeters, tolerance: 1);
        Assert.Equal(TimeSpan.FromSeconds(10), stats.TrackedTime);
    }

    [Fact]
    public void Calculate_ImpossibleJump_IsNotCountedAsTravel()
    {
        // 2,800 km in 5 s (an emulator teleport or GPS glitch) must not inflate the distance.
        var points = new[]
        {
            Point(0, 0),
            Point(100, 5),
            Point(2_800_000, 10),
            Point(2_800_100, 15),
        };

        var stats = TrackingStats.Calculate(points);

        Assert.Equal(200, stats.DistanceMeters, tolerance: 1);
        Assert.Equal(TimeSpan.FromSeconds(10), stats.TrackedTime);
    }

    [Fact]
    public void Calculate_HighwaySpeed_IsCountedAsTravel()
    {
        // 150 m in 5 s = 108 km/h: plausible.
        var points = new[] { Point(0, 0), Point(150, 5) };

        Assert.Equal(150, TrackingStats.Calculate(points).DistanceMeters, tolerance: 1);
    }

    [Fact]
    public void Calculate_FindsLongestStop()
    {
        var points = new List<LocationPoint>();
        points.AddRange(Enumerable.Range(0, 4).Select(i => Point(0, i * 5)));             // 15 s stop at 0 m
        points.AddRange(Enumerable.Range(1, 2).Select(i => Point(i * 200, 15 + (i * 5)))); // drive: 200 m, 400 m
        points.AddRange(Enumerable.Range(0, 13).Select(i => Point(600, 35 + (i * 5))));    // 60 s stop at 600 m

        var stats = TrackingStats.Calculate(points);

        Assert.Equal(TimeSpan.FromSeconds(60), stats.LongestStop);
    }

    [Theory]
    [InlineData(0, "0 m")]
    [InlineData(850.4, "850 m")]
    [InlineData(12_400, "12.4 km")]
    public void FormatDistance_UsesMetersThenKilometers(double meters, string expected)
    {
        Assert.Equal(expected, TrackingStats.FormatDistance(meters).Replace(',', '.'));
    }

    [Theory]
    [InlineData(45, "45 s")]
    [InlineData(18 * 60, "18 min")]
    [InlineData(65 * 60, "1 h 05 min")]
    public void FormatDuration_PicksReadableUnit(int seconds, string expected)
    {
        Assert.Equal(expected, TrackingStats.FormatDuration(TimeSpan.FromSeconds(seconds)));
    }

    private static LocationPoint Point(double metersNorth, int seconds) =>
        LocationPoint.Create(40.0 + (metersNorth / MetersPerDegreeLatitude), -89.6, BaseTime.AddSeconds(seconds));
}
