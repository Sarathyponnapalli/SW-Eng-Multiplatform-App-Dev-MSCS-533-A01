using HeatMapTracker.Core.Data;
using HeatMapTracker.Services;
using HeatMapTracker.ViewModels;
using HeatMapTracker.Views;
using Microsoft.Extensions.Logging;

namespace HeatMapTracker;

/// <summary>
/// App entry point: configures .NET MAUI, the Maps control and dependency injection.
/// </summary>
public static class MauiProgram
{
    /// <summary>File name of the SQLite database that stores the location history.</summary>
    private const string DatabaseFileName = "locations.db3";

    /// <summary>Builds the configured <see cref="MauiApp"/>.</summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiMaps()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Data: one SQLite file in the app's private storage, shared for the app's lifetime.
        builder.Services.AddSingleton<ILocationRepository>(_ =>
            new SqliteLocationRepository(Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName)));

        // Device location.
        builder.Services.AddSingleton(Geolocation.Default);
        builder.Services.AddSingleton<ILocationTracker, LocationTracker>();

        // UI.
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<MainView>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
