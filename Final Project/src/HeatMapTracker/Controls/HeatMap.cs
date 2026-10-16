using System.Collections.Specialized;
using HeatMapTracker.Core.Heatmap;
using HeatMapTracker.Core.Models;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Map = Microsoft.Maui.Controls.Maps.Map;

namespace HeatMapTracker.Controls;

/// <summary>
/// A .NET MAUI <see cref="Map"/> extended with a heat-map layer.
/// <para>
/// The stock Maps control has pins, polylines, polygons and circles, but no heat map.
/// This control adds a bindable <see cref="Points"/> property: the readings are binned into grid
/// cells by <see cref="HeatmapBuilder"/>, and each cell is drawn as a translucent <see cref="Circle"/>
/// whose color (blue → green → yellow → red) and size come from <see cref="HeatGradient"/>.
/// Places with more readings, i.e. where the user spent more time, look hotter.
/// </para>
/// It uses only the public map-element API, so it works on every platform the Maps control supports
/// and keeps all of the stock map's features (pan, zoom, user location, other map elements).
/// </summary>
public class HeatMap : Map
{
    /// <summary>How long to wait after a change before redrawing, so bursts of points cause one redraw.</summary>
    private static readonly TimeSpan RedrawDelay = TimeSpan.FromSeconds(1);

    // Zoom scaling, as fractions of the visible region's radius (center to edge).
    // E.g. viewing a 5 km radius gives 100 m cells and circle radii of 60–150 m.
    private const double ZoomCellFactor = 0.02;
    private const double ZoomMinRadiusFactor = 0.012;
    private const double ZoomMaxRadiusFactor = 0.03;

    /// <summary>Bindable property for <see cref="Points"/>.</summary>
    public static readonly BindableProperty PointsProperty = BindableProperty.Create(
        nameof(Points),
        typeof(IEnumerable<LocationPoint>),
        typeof(HeatMap),
        propertyChanged: OnPointsChanged);

    /// <summary>Bindable property for <see cref="CellSizeMeters"/>.</summary>
    public static readonly BindableProperty CellSizeMetersProperty = BindableProperty.Create(
        nameof(CellSizeMeters),
        typeof(double),
        typeof(HeatMap),
        HeatmapBuilder.DefaultCellSizeMeters,
        validateValue: (_, value) => value is double size && size > 0,
        propertyChanged: (bindable, _, _) => ((HeatMap)bindable).Redraw());

    /// <summary>Bindable property for <see cref="FillOpacity"/>.</summary>
    public static readonly BindableProperty FillOpacityProperty = BindableProperty.Create(
        nameof(FillOpacity),
        typeof(double),
        typeof(HeatMap),
        0.55,
        validateValue: (_, value) => value is double opacity && opacity is >= 0 and <= 1,
        propertyChanged: (bindable, _, _) => ((HeatMap)bindable).Redraw());

    /// <summary>Circles this control added, so redraws never remove map elements added by others.</summary>
    private readonly List<Circle> _heatCircles = [];

    private IDispatcherTimer? _redrawTimer;

    /// <summary>
    /// Location readings to visualize. If the collection implements
    /// <see cref="INotifyCollectionChanged"/> (e.g. an ObservableCollection), the heat map updates live.
    /// </summary>
    public IEnumerable<LocationPoint>? Points
    {
        get => (IEnumerable<LocationPoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    /// <summary>Approximate width of one heat cell on the ground, in meters. Default 25.</summary>
    public double CellSizeMeters
    {
        get => (double)GetValue(CellSizeMetersProperty);
        set => SetValue(CellSizeMetersProperty, value);
    }

    /// <summary>Opacity of each circle's fill, from 0 to 1. Default 0.55 so the streets stay readable.</summary>
    public double FillOpacity
    {
        get => (double)GetValue(FillOpacityProperty);
        set => SetValue(FillOpacityProperty, value);
    }

    /// <summary>Rebuilds the heat circles from the current <see cref="Points"/> immediately.</summary>
    public void Redraw()
    {
        foreach (var circle in _heatCircles)
        {
            MapElements.Remove(circle);
        }

        _heatCircles.Clear();

        // Scale with zoom, like production heat-map layers: when zoomed out, aggregate into larger
        // cells and draw larger circles so the heat stays visible; when zoomed in, show fine detail.
        var viewRadiusMeters = VisibleRegion?.Radius.Meters ?? 0;
        var cellSize = Math.Max(CellSizeMeters, viewRadiusMeters * ZoomCellFactor);
        var minRadius = Math.Max(HeatGradient.DefaultMinRadiusMeters, viewRadiusMeters * ZoomMinRadiusFactor);
        var maxRadius = Math.Max(HeatGradient.DefaultMaxRadiusMeters, viewRadiusMeters * ZoomMaxRadiusFactor);

        foreach (var cell in HeatmapBuilder.Build(Points, cellSize))
        {
            var circle = CreateCircle(cell, minRadius, maxRadius);
            _heatCircles.Add(circle);
            MapElements.Add(circle);
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        // The user zoomed or panned: re-aggregate for the new scale (debounced).
        if (propertyName == nameof(VisibleRegion))
        {
            ScheduleRedraw();
        }
    }

    private Circle CreateCircle(HeatCell cell, double minRadiusMeters, double maxRadiusMeters)
    {
        var (r, g, b) = HeatGradient.ColorFor(cell.Intensity);
        return new Circle
        {
            Center = new Location(cell.Latitude, cell.Longitude),
            Radius = Distance.FromMeters(HeatGradient.RadiusMetersFor(cell.Intensity, minRadiusMeters, maxRadiusMeters)),
            FillColor = Color.FromRgba(r, g, b, (byte)Math.Round(FillOpacity * 255)),
            StrokeColor = Color.FromRgba(r, g, b, (byte)230),
            StrokeWidth = 1,
        };
    }

    private static void OnPointsChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var map = (HeatMap)bindable;

        if (oldValue is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= map.OnPointsCollectionChanged;
        }

        if (newValue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += map.OnPointsCollectionChanged;
        }

        // A whole new collection (e.g. history loaded from the database) is drawn right away.
        map.Redraw();
    }

    private void OnPointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            // Cleared: update immediately so "Clear history" feels instant.
            _redrawTimer?.Stop();
            Redraw();
            return;
        }

        ScheduleRedraw();
    }

    /// <summary>
    /// Coalesces rapid changes into a single redraw after <see cref="RedrawDelay"/>.
    /// Redrawing works on aggregated cells, not raw points, so it stays cheap as history grows.
    /// </summary>
    private void ScheduleRedraw()
    {
        if (Dispatcher is null)
        {
            Redraw();
            return;
        }

        if (_redrawTimer is null)
        {
            _redrawTimer = Dispatcher.CreateTimer();
            _redrawTimer.Interval = RedrawDelay;
            _redrawTimer.IsRepeating = false;
            _redrawTimer.Tick += (_, _) =>
            {
                // Stop explicitly: not every platform clears IsRunning after a one-shot tick,
                // and a timer stuck "running" would block all future redraws.
                _redrawTimer.Stop();
                Redraw();
            };
        }

        if (!_redrawTimer.IsRunning)
        {
            _redrawTimer.Start();
        }
    }
}
