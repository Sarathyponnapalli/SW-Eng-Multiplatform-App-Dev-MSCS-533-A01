using HeatMapTracker.Core.Data;
using HeatMapTracker.Core.Models;

namespace HeatMapTracker.Core.Tests;

/// <summary>
/// Tests for <see cref="SqliteLocationRepository"/> against a real SQLite file per test.
/// </summary>
public sealed class SqliteLocationRepositoryTests : IAsyncLifetime
{
    private static readonly DateTime BaseTime = new(2026, 10, 15, 18, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"heatmap-tests-{Guid.NewGuid():N}.db3");

    private SqliteLocationRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _repository = new SqliteLocationRepository(_databasePath);
        await _repository.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _repository.DisposeAsync();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    [Fact]
    public async Task GetAllAsync_ReturnsPointsOldestFirst()
    {
        await _repository.AddAsync(LocationPoint.Create(37.0002, -122.0, BaseTime.AddSeconds(10)));
        await _repository.AddAsync(LocationPoint.Create(37.0001, -122.0, BaseTime));
        await _repository.AddAsync(LocationPoint.Create(37.0003, -122.0, BaseTime.AddSeconds(20)));

        var points = await _repository.GetAllAsync();

        Assert.Equal([37.0001, 37.0002, 37.0003], points.Select(p => p.Latitude));
    }

    [Fact]
    public async Task AddAsync_AssignsIdAndRoundTripsAllFields()
    {
        var id = await _repository.AddAsync(LocationPoint.Create(51.5007, -0.1246, BaseTime, accuracyMeters: 8.5));

        var saved = Assert.Single(await _repository.GetAllAsync());
        Assert.True(id > 0);
        Assert.Equal(id, saved.Id);
        Assert.Equal(51.5007, saved.Latitude);
        Assert.Equal(-0.1246, saved.Longitude);
        Assert.Equal(8.5, saved.AccuracyMeters);
        Assert.Equal(BaseTime, saved.TimestampUtc);
        Assert.Equal(DateTimeKind.Utc, saved.TimestampUtc.Kind);
    }

    [Fact]
    public async Task CountAsync_ReflectsNumberOfSavedPoints()
    {
        Assert.Equal(0, await _repository.CountAsync());

        for (var i = 0; i < 5; i++)
        {
            await _repository.AddAsync(LocationPoint.Create(10, 20, BaseTime.AddSeconds(i)));
        }

        Assert.Equal(5, await _repository.CountAsync());
    }

    [Fact]
    public async Task ClearAsync_RemovesAllPoints()
    {
        await _repository.AddAsync(LocationPoint.Create(10, 20, BaseTime));
        await _repository.AddAsync(LocationPoint.Create(11, 21, BaseTime));

        await _repository.ClearAsync();

        Assert.Equal(0, await _repository.CountAsync());
    }

    [Fact]
    public async Task SavedPoints_SurviveReopeningTheDatabase()
    {
        // Simulates an app restart: a brand-new repository instance on the same file.
        await _repository.AddAsync(LocationPoint.Create(48.7, -3.98, BaseTime));
        await _repository.AddAsync(LocationPoint.Create(48.6, -3.95, BaseTime.AddSeconds(5)));
        await _repository.DisposeAsync();

        _repository = new SqliteLocationRepository(_databasePath);
        var points = await _repository.GetAllAsync();

        Assert.Equal(2, points.Count);
        Assert.Equal(48.7, points[0].Latitude);
    }

    [Fact]
    public async Task InitializeAsync_IsIdempotent()
    {
        await _repository.AddAsync(LocationPoint.Create(10, 20, BaseTime));

        await _repository.InitializeAsync();
        await _repository.InitializeAsync();

        Assert.Equal(1, await _repository.CountAsync());
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    [InlineData(double.NaN, 0)]
    public async Task AddAsync_InvalidCoordinates_Throws(double latitude, double longitude)
    {
        var point = new LocationPoint { Latitude = latitude, Longitude = longitude, TimestampUtc = BaseTime };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _repository.AddAsync(point));
        Assert.Equal(0, await _repository.CountAsync());
    }

    [Fact]
    public async Task AddAsync_NullPoint_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.AddAsync(null!));
    }

    [Fact]
    public void Constructor_BlankPath_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => new SqliteLocationRepository(" "));
    }
}
