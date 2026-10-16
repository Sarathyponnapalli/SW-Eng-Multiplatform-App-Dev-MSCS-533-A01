using HeatMapTracker.Core.Models;

namespace HeatMapTracker.Core.Data;

/// <summary>
/// Persistent store for the user's location history.
/// </summary>
public interface ILocationRepository
{
    /// <summary>Creates the storage schema if it does not exist. Safe to call more than once.</summary>
    Task InitializeAsync();

    /// <summary>Saves a point and returns its assigned <see cref="LocationPoint.Id"/>.</summary>
    Task<int> AddAsync(LocationPoint point);

    /// <summary>Returns every saved point, oldest first.</summary>
    Task<IReadOnlyList<LocationPoint>> GetAllAsync();

    /// <summary>Returns the number of saved points.</summary>
    Task<int> CountAsync();

    /// <summary>Deletes all saved points.</summary>
    Task ClearAsync();
}
