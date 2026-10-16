using HeatMapTracker.Core.Models;
using SQLite;

namespace HeatMapTracker.Core.Data;

/// <summary>
/// SQLite-backed <see cref="ILocationRepository"/> using sqlite-net.
/// The connection is opened lazily on first use, so constructing the repository is cheap
/// and can safely happen during dependency-injection setup.
/// </summary>
public sealed class SqliteLocationRepository : ILocationRepository, IAsyncDisposable
{
    private const SQLiteOpenFlags OpenFlags =
        SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache;

    private readonly string _databasePath;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private SQLiteAsyncConnection? _connection;

    /// <summary>Creates a repository that stores data in the given database file.</summary>
    /// <param name="databasePath">Full path to the SQLite file; it is created if missing.</param>
    /// <exception cref="ArgumentException"><paramref name="databasePath"/> is null or blank.</exception>
    public SqliteLocationRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = databasePath;
    }

    /// <inheritdoc />
    public async Task InitializeAsync() => await GetConnectionAsync().ConfigureAwait(false);

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="point"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The point has invalid coordinates.</exception>
    public async Task<int> AddAsync(LocationPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);
        point.Validate();
        point.TimestampUtc = point.TimestampUtc.ToUniversalTime();

        var connection = await GetConnectionAsync().ConfigureAwait(false);
        await connection.InsertAsync(point).ConfigureAwait(false);
        return point.Id;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LocationPoint>> GetAllAsync()
    {
        var connection = await GetConnectionAsync().ConfigureAwait(false);
        var points = await connection.Table<LocationPoint>()
            .OrderBy(p => p.TimestampUtc)
            .ToListAsync()
            .ConfigureAwait(false);

        // sqlite-net stores DateTime as ticks and reads it back as Unspecified; restore the UTC kind.
        foreach (var point in points)
        {
            point.TimestampUtc = DateTime.SpecifyKind(point.TimestampUtc, DateTimeKind.Utc);
        }

        return points;
    }

    /// <inheritdoc />
    public async Task<int> CountAsync()
    {
        var connection = await GetConnectionAsync().ConfigureAwait(false);
        return await connection.Table<LocationPoint>().CountAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ClearAsync()
    {
        var connection = await GetConnectionAsync().ConfigureAwait(false);
        await connection.DeleteAllAsync<LocationPoint>().ConfigureAwait(false);
    }

    /// <summary>Closes the underlying SQLite connection.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.CloseAsync().ConfigureAwait(false);
            _connection = null;
        }

        _initLock.Dispose();
    }

    /// <summary>
    /// Opens the connection and creates the table exactly once, even when called concurrently.
    /// </summary>
    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection is not null)
        {
            return _connection;
        }

        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_connection is null)
            {
                var connection = new SQLiteAsyncConnection(_databasePath, OpenFlags);
                await connection.CreateTableAsync<LocationPoint>().ConfigureAwait(false);
                _connection = connection;
            }

            return _connection;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
