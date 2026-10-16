using HeatMapTracker.Core.Heatmap;
using HeatMapTracker.Core.Models;

namespace HeatMapTracker.Core.Tests;

/// <summary>
/// Tests for <see cref="HeatmapBuilder"/> and <see cref="HeatGradient"/>.
/// </summary>
public sealed class HeatmapTests
{
    private const double MetersPerDegreeLatitude = 111_320d;
    private static readonly DateTime BaseTime = new(2026, 10, 15, 18, 0, 0, DateTimeKind.Utc);

    // A coordinate in the middle of a 25 m grid cell, so small offsets stay inside it.
    private static readonly double CellLatitude = CellCenter(37.3318, HeatmapBuilder.DefaultCellSizeMeters / MetersPerDegreeLatitude);
    private const double CellLongitude = -122.0312;

    [Fact]
    public void Build_NullOrEmpty_ReturnsNoCells()
    {
        Assert.Empty(HeatmapBuilder.Build(null));
        Assert.Empty(HeatmapBuilder.Build([]));
    }

    [Fact]
    public void Build_PointsAtSameSpot_MergeIntoOneCell()
    {
        var points = Enumerable.Range(0, 4).Select(i => Point(CellLatitude, CellLongitude, i)).ToList();

        var cell = Assert.Single(HeatmapBuilder.Build(points));

        Assert.Equal(4, cell.Count);
        Assert.Equal(1d, cell.Intensity, precision: 10);
    }

    [Fact]
    public void Build_PointsAFewMetersApart_MergeAndUseCentroid()
    {
        var offset = 3 / MetersPerDegreeLatitude; // about 3 m north/south
        var points = new[]
        {
            Point(CellLatitude - offset, CellLongitude, 0),
            Point(CellLatitude + offset, CellLongitude, 1),
        };

        var cell = Assert.Single(HeatmapBuilder.Build(points));

        Assert.Equal(2, cell.Count);
        Assert.Equal(CellLatitude, cell.Latitude, precision: 9);
        Assert.Equal(CellLongitude, cell.Longitude, precision: 9);
    }

    [Fact]
    public void Build_Points100MetersApart_StayInSeparateCells()
    {
        var hundredMetersNorth = CellLatitude + (100 / MetersPerDegreeLatitude);
        var points = new[] { Point(CellLatitude, CellLongitude, 0), Point(hundredMetersNorth, CellLongitude, 1) };

        Assert.Equal(2, HeatmapBuilder.Build(points).Count);
    }

    [Fact]
    public void Build_BusiestCellHasIntensityOne_AndIntensityGrowsWithCount()
    {
        var quiet = CellLatitude;
        var medium = CellLatitude + (200 / MetersPerDegreeLatitude);
        var busy = CellLatitude + (400 / MetersPerDegreeLatitude);
        var points = new List<LocationPoint> { Point(quiet, CellLongitude, 0) };
        points.AddRange(Enumerable.Range(0, 4).Select(i => Point(medium, CellLongitude, i)));
        points.AddRange(Enumerable.Range(0, 16).Select(i => Point(busy, CellLongitude, i)));

        var cells = HeatmapBuilder.Build(points).OrderBy(c => c.Count).ToList();

        Assert.Equal([1, 4, 16], cells.Select(c => c.Count));
        Assert.Equal(1d, cells[2].Intensity, precision: 10);
        Assert.True(cells[0].Intensity < cells[1].Intensity);
        Assert.True(cells[1].Intensity < cells[2].Intensity);
        Assert.All(cells, c => Assert.InRange(c.Intensity, 0d, 1d));
    }

    [Fact]
    public void Build_LogScale_KeepsSingleVisitCellsVisible()
    {
        // One 100-reading stop must not push a single pass-through reading to near-zero heat.
        var points = new List<LocationPoint> { Point(CellLatitude, CellLongitude, 0) };
        var stop = CellLatitude + (200 / MetersPerDegreeLatitude);
        points.AddRange(Enumerable.Range(0, 100).Select(i => Point(stop, CellLongitude, i)));

        var single = HeatmapBuilder.Build(points).Single(c => c.Count == 1);

        Assert.True(single.Intensity > 0.1, $"Expected visible heat, got {single.Intensity:F3}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    public void Build_NonPositiveCellSize_Throws(double cellSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HeatmapBuilder.Build([], cellSize));
    }

    [Fact]
    public void ColorFor_EndpointsAndStops_AreExact()
    {
        Assert.Equal(((byte)0, (byte)0, (byte)255), HeatGradient.ColorFor(0));
        Assert.Equal(((byte)0, (byte)255, (byte)0), HeatGradient.ColorFor(1d / 3d));
        Assert.Equal(((byte)255, (byte)255, (byte)0), HeatGradient.ColorFor(2d / 3d));
        Assert.Equal(((byte)255, (byte)0, (byte)0), HeatGradient.ColorFor(1));
    }

    [Fact]
    public void ColorFor_Midway_Interpolates()
    {
        // Halfway between blue (0,0,255) and green (0,255,0).
        Assert.Equal(((byte)0, (byte)128, (byte)128), HeatGradient.ColorFor(1d / 6d));
    }

    [Theory]
    [InlineData(-1, 0, 0, 255)]
    [InlineData(2, 255, 0, 0)]
    [InlineData(double.NaN, 0, 0, 255)]
    public void ColorFor_OutOfRange_IsClamped(double intensity, byte r, byte g, byte b)
    {
        Assert.Equal((r, g, b), HeatGradient.ColorFor(intensity));
    }

    [Fact]
    public void RadiusMetersFor_GrowsWithIntensity()
    {
        Assert.Equal(HeatGradient.DefaultMinRadiusMeters, HeatGradient.RadiusMetersFor(0));
        Assert.Equal(HeatGradient.DefaultMaxRadiusMeters, HeatGradient.RadiusMetersFor(1));
        Assert.Equal(21d, HeatGradient.RadiusMetersFor(0.5), precision: 10);
        Assert.Equal(HeatGradient.DefaultMaxRadiusMeters, HeatGradient.RadiusMetersFor(5));
    }

    [Fact]
    public void RadiusMetersFor_InvalidRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HeatGradient.RadiusMetersFor(0.5, minMeters: 10, maxMeters: 5));
    }

    private static LocationPoint Point(double latitude, double longitude, int secondsOffset) =>
        LocationPoint.Create(latitude, longitude, BaseTime.AddSeconds(secondsOffset * 5));

    private static double CellCenter(double latitude, double cellHeightDegrees) =>
        (Math.Floor(latitude / cellHeightDegrees) + 0.5) * cellHeightDegrees;
}
