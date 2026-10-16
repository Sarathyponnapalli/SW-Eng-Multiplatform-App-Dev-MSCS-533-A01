namespace HeatMapTracker.Core.Heatmap;

/// <summary>
/// One aggregated area of the heat map.
/// </summary>
/// <param name="Latitude">Centroid latitude of the readings in this cell, so the heat sits on the actual path.</param>
/// <param name="Longitude">Centroid longitude of the readings in this cell.</param>
/// <param name="Count">Number of readings that fell into this cell.</param>
/// <param name="Intensity">Relative heat from 0 (coolest) to 1 (hottest cell on the map).</param>
public sealed record HeatCell(double Latitude, double Longitude, int Count, double Intensity);
