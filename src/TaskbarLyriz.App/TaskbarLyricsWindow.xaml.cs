using System.ComponentModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using TaskbarLyriz.App.ViewModels;
using TaskbarLyriz.Core.Abstractions;
using TaskbarLyriz.Core.Configuration;
using TaskbarLyriz.Core.Taskbar;
using TaskbarLyriz.Windows.Taskbar;

namespace TaskbarLyriz.App;

public sealed partial class TaskbarLyricsWindow : Window, IDisposable
{
    private readonly LyricsViewModel _lyrics;
    private readonly IAppSettingsService _settings;
    private readonly IAppLogger _logger;
    private readonly TaskbarWindowController _windowController;
    private readonly nint _windowHandle;
    private readonly DispatcherQueueTimer _placementHeartbeat;
    private AppSettings _currentSettings;
    private bool _isShown;
    private bool _reportedUnsupportedEdge;
    private bool _disposed;

    public TaskbarLyricsWindow(
        LyricsViewModel lyrics,
        IAppSettingsService settings,
        IAppLogger logger,
        TaskbarWindowController windowController)
    {
        _lyrics = lyrics;
        _settings = settings;
        _logger = logger;
        _windowController = windowController;
        _currentSettings = settings.Current;

        InitializeComponent();
        OverlaySurface.DataContext = lyrics;
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }

        _windowController.Configure(_windowHandle);
        _windowController.Hide(_windowHandle);
        _placementHeartbeat = DispatcherQueue.CreateTimer();
        _placementHeartbeat.Interval = TimeSpan.FromSeconds(2);
        _placementHeartbeat.IsRepeating = true;
        _placementHeartbeat.Tick += OnPlacementHeartbeat;
        _placementHeartbeat.Start();
        _lyrics.PropertyChanged += OnLyricsPropertyChanged;
        _lyrics.CurrentLineChanged += OnCurrentLineChanged;
        _settings.Changed += OnSettingsChanged;
        ApplySettings(_currentSettings);
        UpdateVisibility();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _placementHeartbeat.Stop();
        _placementHeartbeat.Tick -= OnPlacementHeartbeat;
        _lyrics.PropertyChanged -= OnLyricsPropertyChanged;
        _lyrics.CurrentLineChanged -= OnCurrentLineChanged;
        _settings.Changed -= OnSettingsChanged;
        _windowController.Release(_windowHandle);
        Close();
    }

    public void RefreshPlacement()
    {
        if (_disposed)
        {
            return;
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            UpdateVisibility();
        }
        else
        {
            DispatcherQueue.TryEnqueue(UpdateVisibility);
        }
    }

    private void OnPlacementHeartbeat(DispatcherQueueTimer sender, object args) => UpdateVisibility();

    private void OnLyricsPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(LyricsViewModel.PlaybackState) or
            nameof(LyricsViewModel.HasSynchronizedLyrics) or
            nameof(LyricsViewModel.TaskbarText))
        {
            UpdateVisibility();
        }
    }

    private void OnCurrentLineChanged(object? sender, CurrentLyricLineChangedEventArgs args)
    {
        UpdateVisibility();
        if (!_isShown || _currentSettings.Appearance.Animation == LyricAnimation.None)
        {
            CurrentLyricText.Opacity = 1;
            CurrentLyricTranslate.X = 0;
            return;
        }

        var duration = new Duration(TimeSpan.FromMilliseconds(180));
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var animation = new DoubleAnimation
        {
            From = 0.35,
            To = 1,
            Duration = duration,
            EnableDependentAnimation = true,
            EasingFunction = easing,
        };
        Storyboard.SetTarget(animation, CurrentLyricText);
        Storyboard.SetTargetProperty(animation, "Opacity");
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        if (_currentSettings.Appearance.Animation is LyricAnimation.Slide or LyricAnimation.Karaoke)
        {
            var slide = new DoubleAnimation
            {
                From = 8,
                To = 0,
                Duration = duration,
                EnableDependentAnimation = true,
                EasingFunction = easing,
            };
            Storyboard.SetTarget(slide, CurrentLyricTranslate);
            Storyboard.SetTargetProperty(slide, "X");
            storyboard.Children.Add(slide);
        }

        storyboard.Begin();
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            ApplySettings(settings);
            UpdateVisibility();
        }
        else
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ApplySettings(settings);
                UpdateVisibility();
            });
        }
    }

    private void ApplySettings(AppSettings settings)
    {
        _currentSettings = settings;
        OverlaySurface.RequestedTheme = settings.Appearance.Theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        OverlaySurface.Opacity = settings.Appearance.Opacity;
        OverlaySurface.CornerRadius = new CornerRadius(settings.Appearance.CornerRadius);
        CurrentLyricText.FontFamily = new FontFamily(settings.Appearance.FontFamily);
        CurrentLyricText.FontSize = settings.Appearance.FontSize;
    }

    private void UpdateVisibility()
    {
        if (_disposed)
        {
            return;
        }

        var shouldShow = TaskbarLyricsVisibilityPolicy.ShouldShow(
            _currentSettings.TaskbarLyrics.Enabled,
            !string.IsNullOrWhiteSpace(_lyrics.TaskbarText),
            _lyrics.PlaybackState);
        if (!shouldShow)
        {
            if (_isShown)
            {
                _windowController.Hide(_windowHandle);
                _isShown = false;
                _logger.Information("taskbar.hidden", "Taskbar lyrics were hidden.");
            }

            return;
        }

        try
        {
            var showResult = _windowController.PositionAndShow(
                _windowHandle,
                _currentSettings.TaskbarLyrics.Position,
                _currentSettings.TaskbarLyrics.Width,
                Math.Clamp(_currentSettings.Appearance.FontSize + 25, 40, 48));
            if (showResult != TaskbarWindowShowResult.Shown)
            {
                // Keep the current surface alive while shell geometry is transiently unavailable.
                if (!_isShown)
                {
                    _windowController.Hide(_windowHandle);
                }

                if (!_reportedUnsupportedEdge)
                {
                    _reportedUnsupportedEdge = true;
                    if (showResult == TaskbarWindowShowResult.UnsupportedTaskbarEdge)
                    {
                        _logger.Warning(
                            "taskbar.unsupported_edge",
                            "Milestone 5 supports the primary monitor with a bottom taskbar.");
                    }
                    else
                    {
                        _logger.Warning(
                            "taskbar.no_safe_space",
                            "Taskbar lyrics were hidden because no unused taskbar gap was large enough.");
                    }
                }

                return;
            }

            _reportedUnsupportedEdge = false;
            if (!_isShown)
            {
                _isShown = true;
                _logger.Information(
                    "taskbar.shown",
                    "Taskbar lyrics were shown inside an unused taskbar region without activating the window.");
            }
        }
        catch (Exception exception)
        {
            _windowController.Hide(_windowHandle);
            _isShown = false;
            _logger.Error(
                "taskbar.window_failed",
                "The taskbar lyrics window could not be positioned or shown.",
                exception);
        }
    }
}
