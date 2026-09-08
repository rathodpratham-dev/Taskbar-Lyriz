using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using TaskbarLyriz.App.ViewModels;
using TaskbarLyriz.Core.Abstractions;
using TaskbarLyriz.Core.Lyrics;
using TaskbarLyriz.Core.Models;
using TaskbarLyriz.Infrastructure.Lyrics;
using TaskbarLyriz.Infrastructure.LyricsProviders;
using TaskbarLyriz.Infrastructure.Logging;
using TaskbarLyriz.Infrastructure.Settings;
using TaskbarLyriz.Infrastructure.Storage;
using TaskbarLyriz.Windows.Lifecycle;
using TaskbarLyriz.Windows.MediaSession;
using TaskbarLyriz.Windows.Shell;
using TaskbarLyriz.Windows.Taskbar;

namespace TaskbarLyriz.App;

public partial class App : Application
{
    private ServiceProvider? _services;
    private MainWindow? _mainWindow;
    private TaskbarLyricsWindow? _taskbarLyricsWindow;
    private string _currentSong = "Nothing playing";
    private bool _isExiting;

    public App()
    {
        InitializeComponent();
        _services = ConfigureServices();
        UnhandledException += OnUnhandledException;
        AppInstance.GetCurrent().Activated += OnInstanceActivated;
    }

    internal static T GetService<T>() where T : notnull
    {
        var app = (App)Current;
        var services = app._services
            ?? throw new InvalidOperationException("Application services are not available.");
        return services.GetRequiredService<T>();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var logger = GetService<IAppLogger>();
        try
        {
            var settings = GetService<IAppSettingsService>();
            await settings.InitializeAsync();
            await RepairRequestedStartupRegistrationAsync(settings, logger);

            _mainWindow = new MainWindow(settings, logger);
            _mainWindow.ExitRequested += OnExitRequested;
            _mainWindow.Activated += OnMainWindowActivated;

            var lyrics = GetService<LyricsViewModel>();
            _taskbarLyricsWindow = new TaskbarLyricsWindow(
                lyrics,
                settings,
                logger,
                GetService<TaskbarWindowController>());
            settings.Changed += OnSettingsChanged;

            var tray = GetService<ITrayIconService>();
            tray.ShowRequested += OnShowRequested;
            tray.SettingsRequested += OnSettingsRequested;
            tray.TaskbarLyricsToggleRequested += OnTaskbarLyricsToggleRequested;
            tray.ExitRequested += OnExitRequested;
            tray.Initialize();
            UpdateTrayState(settings.Current.TaskbarLyrics.Enabled);

            logger.Information("application.started", "TaskbarLyriz started.");
            _mainWindow.Activate();

            if (settings.Current.General.StartMinimized)
            {
                _mainWindow.HideToTray();
            }

            var mediaSessions = GetService<IMediaSessionService>();
            mediaSessions.CurrentSessionChanged += OnCurrentMediaSessionChanged;
            await mediaSessions.StartAsync();
        }
        catch (Exception exception)
        {
            logger.Error("application.start_failed", "TaskbarLyriz could not start.", exception);
            throw;
        }
    }

    private ServiceProvider ConfigureServices()
    {
        var paths = new AppPaths();
        var logger = new JsonFileLogger(paths);
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");

        var services = new ServiceCollection();
        services.AddSingleton(paths);
        services.AddSingleton<IAppLogger>(logger);
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IStartupService, WindowsStartupService>();
        services.AddSingleton<ITrayIconService>(_ => new TrayIconService(iconPath, logger));
        services.AddSingleton<IMediaSessionService, GsmTcMediaSessionService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(_ =>
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TaskbarLyriz/0.5");
            return client;
        });
        services.AddSingleton<ILrcParser, LrcParser>();
        services.AddSingleton<ILyricsSynchronizationService, LyricsSynchronizationService>();
        services.AddSingleton<ILyricsCache, SqliteLyricsCache>();
        services.AddSingleton<ILyricsProvider, LrcLibLyricsProvider>();
        services.AddSingleton<ILyricsProvider, GeniusLyricsProvider>();
        services.AddSingleton<ILyricsService, LyricsService>();
        services.AddSingleton<TaskbarWindowController>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MediaSessionViewModel>();
        services.AddSingleton<LyricsViewModel>();
        services.AddSingleton<CacheViewModel>();
        return services.BuildServiceProvider();
    }

    private static async Task RepairRequestedStartupRegistrationAsync(
        IAppSettingsService settings,
        IAppLogger logger)
    {
        if (!settings.Current.General.StartWithWindows)
        {
            return;
        }

        var startup = GetService<IStartupService>();
        var status = await startup.GetStatusAsync();
        if (status != StartupStatus.Disabled)
        {
            return;
        }

        var result = await startup.SetEnabledAsync(enabled: true);
        if (result.Succeeded)
        {
            logger.Information(
                "startup.registration_repaired",
                "The requested Windows startup registration was updated for this installation.");
        }
        else
        {
            logger.Warning(
                "startup.registration_repair_failed",
                result.Message ?? "The requested Windows startup registration could not be updated.");
        }
    }

    private void OnInstanceActivated(object? sender, AppActivationArguments args)
    {
        _mainWindow?.DispatcherQueue.TryEnqueue(() => _mainWindow.ShowAndActivate());
    }

    private void OnShowRequested(object? sender, EventArgs args)
    {
        _mainWindow?.ShowAndActivate();
    }

    private void OnMainWindowActivated(object sender, WindowActivatedEventArgs args) =>
        _taskbarLyricsWindow?.RefreshPlacement();

    private void OnSettingsRequested(object? sender, EventArgs args)
    {
        _mainWindow?.ShowAndActivate();
        _mainWindow?.NavigateToSettings();
    }

    private async void OnTaskbarLyricsToggleRequested(object? sender, EventArgs args)
    {
        var logger = GetService<IAppLogger>();
        try
        {
            var settings = GetService<IAppSettingsService>();
            await settings.UpdateAsync(current => current with
            {
                TaskbarLyrics = current.TaskbarLyrics with
                {
                    Enabled = !current.TaskbarLyrics.Enabled,
                },
            });
        }
        catch (Exception exception)
        {
            logger.Error(
                "settings.taskbar_toggle_failed",
                "The taskbar lyrics setting could not be changed from the tray.",
                exception);
        }
    }

    private void UpdateTrayState(bool taskbarLyricsEnabled)
    {
        GetService<ITrayIconService>().Update(new TrayIconState(taskbarLyricsEnabled, _currentSong));
    }

    private void OnSettingsChanged(object? sender, Core.Configuration.AppSettings settings)
    {
        _mainWindow?.DispatcherQueue.TryEnqueue(() =>
            UpdateTrayState(settings.TaskbarLyrics.Enabled));
    }

    private void OnCurrentMediaSessionChanged(
        object? sender,
        CurrentMediaSessionChangedEventArgs args)
    {
        var session = args.Session;
        var currentSong = session is null
            ? "Nothing playing"
            : string.IsNullOrWhiteSpace(session.Track.Artist)
                ? session.DisplayTitle
                : $"{session.DisplayTitle} \u2014 {session.Track.Artist}";
        if (string.Equals(_currentSong, currentSong, StringComparison.Ordinal))
        {
            return;
        }

        _currentSong = currentSong;
        UpdateTrayState(GetService<IAppSettingsService>().Current.TaskbarLyrics.Enabled);
    }

    private void OnExitRequested(object? sender, EventArgs args)
    {
        ExitApplication();
    }

    private async void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        var logger = GetService<IAppLogger>();
        logger.Information("application.stopping", "TaskbarLyriz is stopping.");
        var settings = GetService<IAppSettingsService>();
        settings.Changed -= OnSettingsChanged;
        _taskbarLyricsWindow?.Dispose();
        _taskbarLyricsWindow = null;
        GetService<ITrayIconService>().Dispose();
        GetService<IMediaSessionService>().CurrentSessionChanged -= OnCurrentMediaSessionChanged;
        var services = _services;
        if (services is not null)
        {
            await services.DisposeAsync();
        }

        _services = null;
        if (_mainWindow is not null)
        {
            _mainWindow.Activated -= OnMainWindowActivated;
        }

        _mainWindow?.AllowCloseAndClose();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
    {
        try
        {
            GetService<IAppLogger>().Error(
                "application.unhandled_exception",
                "An unhandled UI exception occurred.",
                args.Exception);
        }
        catch
        {
            // There is no safe logging fallback during process teardown.
        }
    }
}
