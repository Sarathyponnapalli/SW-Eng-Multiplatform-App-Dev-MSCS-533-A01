namespace HeatMapTracker.Core.Heatmap;

/// <summary>
/// Converts a heat intensity (0 to 1) into the visual style of a heat-map circle.
/// Uses the classic heat-map ramp: blue (cool) → green → yellow → red (hot).
/// Kept free of UI types so it can be unit-tested; the app converts the RGB values to a MAUI color.
/// </summary>
public static class HeatGradient
{
    /// <summary>Gradient stops as (position, red, green, blue), in ascending position order.</summary>
    private static readonly (double Position, byte R, byte G, byte B)[] Stops =
    [
        (0d, 0, 0, 255),        // blue
        (1d / 3d, 0, 255, 0),   // green
        (2d / 3d, 255, 255, 0), // yellow
        (1d, 255, 0, 0),        // red
    ];

    /// <summary>Default radius of the coolest circle, in meters.</summary>
    public const double DefaultMinRadiusMeters = 12d;

    /// <summary>Default radius of the hottest circle, in meters.</summary>
    public const double DefaultMaxRadiusMeters = 30d;

    /// <summary>
    /// Returns the color for <paramref name="intensity"/>, linearly interpolated between gradient stops.
    /// Values outside 0–1 (and NaN) are clamped.
    /// </summary>
    public static (byte R, byte G, byte B) ColorFor(double intensity)
    {
        var t = Clamp01(intensity);

        for (var i = 1; i < Stops.Length; i++)
        {
            var upper = Stops[i];
            if (t <= upper.Position)
            {
                var lower = Stops[i - 1];
                var fraction = (t - lower.Position) / (upper.Position - lower.Position);
                return (Lerp(lower.R, upper.R, fraction), Lerp(lower.G, upper.G, fraction), Lerp(lower.B, upper.B, fraction));
            }
        }

        var last = Stops[^1];
        return (last.R, last.G, last.B);
    }

    /// <summary>
    /// Returns a circle radius that grows linearly from <paramref name="minMeters"/> at intensity 0
    /// to <paramref name="maxMeters"/> at intensity 1, so hot spots read larger as well as redder.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The radii are negative or <paramref name="maxMeters"/> is less than <paramref name="minMeters"/>.</exception>
    public static double RadiusMetersFor(
        double intensity,
        double minMeters = DefaultMinRadiusMeters,
        double maxMeters = DefaultMaxRadiusMeters)
    {
        if (minMeters < 0 || maxMeters < minMeters)
        {
            throw new ArgumentOutOfRangeException(nameof(maxMeters), "Radii must be non-negative and max must be at least min.");
        }

        return minMeters + ((maxMeters - minMeters) * Clamp01(intensity));
    }

    private static double Clamp01(double value) => double.IsNaN(value) ? 0d : Math.Clamp(value, 0d, 1d);

    private static byte Lerp(byte from, byte to, double fraction) =>
        (byte)Math.Round(from + ((to - from) * fraction));
}
