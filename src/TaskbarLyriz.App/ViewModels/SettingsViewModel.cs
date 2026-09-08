using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using TaskbarLyriz.Core.Abstractions;
using TaskbarLyriz.Core.Configuration;
using TaskbarLyriz.Infrastructure.Storage;

namespace TaskbarLyriz.App.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IAppSettingsService _settings;
    private readonly IStartupService _startup;
    private readonly IAppLogger _logger;
    private readonly DispatcherQueue _dispatcherQueue;
    private bool _startWithWindows;
    private bool _startMinimized;
    private bool _minimizeToTray;
    private bool _taskbarLyricsEnabled;
    private int _taskbarPositionIndex;
    private double _taskbarWidth;
    private bool _canChangeStartup = true;
    private int _themeIndex;
    private int _fontFamilyIndex;
    private FontFamily _lyricFontFamily = new(AppearanceSettings.DefaultFontFamily);
    private double _lyricFontSize = 15;
    private double _appearanceOpacityPercent = 94;
    private double _appearanceCornerRadius = 10;
    private int _animationIndex = 1;
    private double _lyricsTimingOffsetMilliseconds;
    private bool _followCurrentLine = true;
    private string _statusMessage = "Settings are saved automatically.";
    private bool _statusIsError;

    public SettingsViewModel(
        IAppSettingsService settings,
        IStartupService startup,
        IAppLogger logger,
        AppPaths paths)
    {
        _settings = settings;
        _startup = startup;
        _logger = logger;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("The settings view model must be created on the UI thread.");
        DataLocation = paths.DataRoot;
        Apply(settings.Current);
        _settings.Changed += OnSettingsChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool StartWithWindows
    {
        get => _startWithWindows;
        private set => SetField(ref _startWithWindows, value);
    }

    public bool StartMinimized
    {
        get => _startMinimized;
        private set => SetField(ref _startMinimized, value);
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        private set => SetField(ref _minimizeToTray, value);
    }

    public bool TaskbarLyricsEnabled
    {
        get => _taskbarLyricsEnabled;
        private set => SetField(ref _taskbarLyricsEnabled, value);
    }

    public int TaskbarPositionIndex
    {
        get => _taskbarPositionIndex;
        private set => SetField(ref _taskbarPositionIndex, value);
    }

    public double TaskbarWidth
    {
        get => _taskbarWidth;
        private set => SetField(ref _taskbarWidth, value);
    }

    public bool CanChangeStartup
    {
        get => _canChangeStartup;
        private set => SetField(ref _canChangeStartup, value);
    }

    public int ThemeIndex
    {
        get => _themeIndex;
        private set => SetField(ref _themeIndex, value);
    }

    public int FontFamilyIndex
    {
        get => _fontFamilyIndex;
        private set => SetField(ref _fontFamilyIndex, value);
    }

    public FontFamily LyricFontFamily
    {
        get => _lyricFontFamily;
        private set => SetField(ref _lyricFontFamily, value);
    }

    public double LyricFontSize
    {
        get => _lyricFontSize;
        private set => SetField(ref _lyricFontSize, value);
    }

    public double AppearanceOpacityPercent
    {
        get => _appearanceOpacityPercent;
        private set => SetField(ref _appearanceOpacityPercent, value);
    }

    public double PreviewOpacity => AppearanceOpacityPercent / 100;

    public double AppearanceCornerRadius
    {
        get => _appearanceCornerRadius;
        private set => SetField(ref _appearanceCornerRadius, value);
    }

    public CornerRadius PreviewCornerRadius => new(AppearanceCornerRadius);

    public int AnimationIndex
    {
        get => _animationIndex;
        private set => SetField(ref _animationIndex, value);
    }

    public double LyricsTimingOffsetMilliseconds
    {
        get => _lyricsTimingOffsetMilliseconds;
        private set => SetField(ref _lyricsTimingOffsetMilliseconds, value);
    }

    public bool FollowCurrentLine
    {
        get => _followCurrentLine;
        private set => SetField(ref _followCurrentLine, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool StatusIsError
    {
        get => _statusIsError;
        private set => SetField(ref _statusIsError, value);
    }

    public string DataLocation { get; }

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
    }

    public async Task RefreshStartupStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = await _startup.GetStatusAsync(cancellationToken);
        StartWithWindows = status is StartupStatus.Enabled or StartupStatus.EnabledByPolicy;
        CanChangeStartup = status is not (StartupStatus.EnabledByPolicy or StartupStatus.DisabledByPolicy);

        if (status == StartupStatus.Unsupported)
        {
            StatusIsError = true;
            StatusMessage = "Windows startup integration is unavailable. See the application log for details.";
        }
        else if (status == StartupStatus.DisabledByUser)
        {
            StatusIsError = true;
            StatusMessage = "Windows has disabled startup. Re-enable TaskbarLyriz in Windows Startup settings.";
        }
        else
        {
            StatusIsError = false;
            StatusMessage = "Settings are saved automatically.";
        }
    }

    public async Task SetStartWithWindowsAsync(bool enabled)
    {
        CanChangeStartup = false;
        var result = await _startup.SetEnabledAsync(enabled);
        StartWithWindows = result.Status is StartupStatus.Enabled or StartupStatus.EnabledByPolicy;
        CanChangeStartup = result.Status is not (StartupStatus.EnabledByPolicy or StartupStatus.DisabledByPolicy);

        if (!result.Succeeded)
        {
            StatusIsError = true;
            StatusMessage = result.Message ?? "Windows could not change the startup setting.";
            return;
        }

        await UpdateAsync(current => current with
        {
            General = current.General with { StartWithWindows = StartWithWindows },
        });
    }

    public Task SetStartMinimizedAsync(bool enabled) => UpdateAsync(current => current with
    {
        General = current.General with { StartMinimized = enabled },
    });

    public Task SetMinimizeToTrayAsync(bool enabled) => UpdateAsync(current => current with
    {
        General = current.General with { MinimizeToTray = enabled },
    });

    public Task SetTaskbarLyricsEnabledAsync(bool enabled) => UpdateAsync(current => current with
    {
        TaskbarLyrics = current.TaskbarLyrics with { Enabled = enabled },
    });

    public Task SetTaskbarPositionAsync(int selectedIndex)
    {
        var position = selectedIndex == 1 ? TaskbarPosition.Right : TaskbarPosition.Left;
        if (position == _settings.Current.TaskbarLyrics.Position)
        {
            return Task.CompletedTask;
        }

        return UpdateAsync(current => current with
        {
            TaskbarLyrics = current.TaskbarLyrics with { Position = position },
        });
    }

    public Task SetTaskbarWidthAsync(double value)
    {
        var width = double.IsFinite(value)
            ? (int)Math.Round(value, MidpointRounding.AwayFromZero)
            : 420;
        width = Math.Clamp(width, 160, 1_200);
        if (width == (int)TaskbarWidth)
        {
            return Task.CompletedTask;
        }

        return UpdateAsync(current => current with
        {
            TaskbarLyrics = current.TaskbarLyrics with { Width = width },
        });
    }

    public Task SetThemeAsync(int selectedIndex)
    {
        var theme = selectedIndex switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.Dark,
            _ => AppTheme.System,
        };

        return UpdateAsync(current => current with
        {
            Appearance = current.Appearance with { Theme = theme },
        });
    }

    public Task SetFontFamilyAsync(int selectedIndex)
    {
        var fontFamily = selectedIndex switch
        {
            1 => "Segoe UI",
            2 => "Arial",
            3 => "Calibri",
            4 => "Consolas",
            _ => AppearanceSettings.DefaultFontFamily,
        };
        if (string.Equals(
            fontFamily,
            _settings.Current.Appearance.FontFamily,
            StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        return UpdateAsync(current => current with
        {
            Appearance = current.Appearance with { FontFamily = fontFamily },
        });
    }

    public Task SetLyricFontSizeAsync(double value)
    {
        var fontSize = double.IsFinite(value)
            ? (int)Math.Round(value, MidpointRounding.AwayFromZero)
            : 15;
        fontSize = Math.Clamp(fontSize, 10, 36);
        if (fontSize == _settings.Current.Appearance.FontSize)
        {
            return Task.CompletedTask;
        }

        return UpdateAsync(current => current with
        {
            Appearance = current.Appearance with { FontSize = fontSize },
        });
    }

    public Task SetAppearanceOpacityAsync(double percent)
    {
        var opacity = double.IsFinite(percent) ? percent / 100 : 0.94;
        opacity = Math.Clamp(opacity, 0.4, 1.0);
        if (Math.Abs(opacity - _settings.Current.Appearance.Opacity) < 0.001)
        {
            return Task.CompletedTask;
        }

        return UpdateAsync(current => current with
        {
            Appearance = current.Appearance with { Opacity = opacity },
        });
    }

    public Task SetAppearanceCornerRadiusAsync(double value)
    {
        var radius = double.IsFinite(value)
            ? (int)Math.Round(value, MidpointRounding.AwayFromZero)
            : 10;
        radius = Math.Clamp(radius, 0, 32);
        if (radius == _settings.Current.Appearance.CornerRadius)
        {
            return Task.CompletedTask;
        }

        return UpdateAsync(current => current with
        {
            Appearance = current.Appearance with { CornerRadius = radius },
        });
    }

    public Task SetAnimationAsync(int selectedIndex)
    {
        var animation = selectedIndex switch
        {
            0 => LyricAnimation.None,
            2 => LyricAnimation.Slide,
            3 => LyricAnimation.Karaoke,
            _ => LyricAnimation.Fade,
        };
        if (animation == _settings.Current.Appearance.Animation)
        {
            return Task.CompletedTask;
        }

        return UpdateAsync(current => current with
        {
            Appearance = current.Appearance with { Animation = animation },
        });
    }

    public Task SetLyricsTimingOffsetAsync(double value)
    {
        var milliseconds = double.IsFinite(value)
            ? (int)Math.Round(value, MidpointRounding.AwayFromZero)
            : 0;
        milliseconds = Math.Clamp(milliseconds, -10_000, 10_000);
        if (milliseconds == (int)LyricsTimingOffsetMilliseconds)
        {
            return Task.CompletedTask;
        }

        return UpdateAsync(current => current with
        {
            Lyrics = current.Lyrics with { TimingOffsetMilliseconds = milliseconds },
        });
    }

    public Task SetFollowCurrentLineAsync(bool enabled)
    {
        if (enabled == FollowCurrentLine)
        {
            return Task.CompletedTask;
        }

        return UpdateAsync(current => current with
        {
            Lyrics = current.Lyrics with { FollowCurrentLine = enabled },
        });
    }

    private async Task UpdateAsync(Func<AppSettings, AppSettings> update)
    {
        try
        {
            await _settings.UpdateAsync(update);
            StatusIsError = false;
            StatusMessage = "Saved.";
        }
        catch (Exception exception)
        {
            StatusIsError = true;
            StatusMessage = "The setting could not be saved. See the application log for details.";
            _logger.Error("settings.save_failed", "A setting could not be saved from the UI.", exception);
        }
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            Apply(settings);
        }
        else
        {
            _dispatcherQueue.TryEnqueue(() => Apply(settings));
        }
    }

    private void Apply(AppSettings settings)
    {
        StartMinimized = settings.General.StartMinimized;
        MinimizeToTray = settings.General.MinimizeToTray;
        TaskbarLyricsEnabled = settings.TaskbarLyrics.Enabled;
        TaskbarPositionIndex = settings.TaskbarLyrics.Position == TaskbarPosition.Right ? 1 : 0;
        TaskbarWidth = settings.TaskbarLyrics.Width;
        ThemeIndex = settings.Appearance.Theme switch
        {
            AppTheme.Light => 1,
            AppTheme.Dark => 2,
            _ => 0,
        };
        FontFamilyIndex = settings.Appearance.FontFamily switch
        {
            "Segoe UI" => 1,
            "Arial" => 2,
            "Calibri" => 3,
            "Consolas" => 4,
            _ => 0,
        };
        LyricFontFamily = new FontFamily(settings.Appearance.FontFamily);
        LyricFontSize = settings.Appearance.FontSize;
        if (SetField(ref _appearanceOpacityPercent, settings.Appearance.Opacity * 100, nameof(AppearanceOpacityPercent)))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PreviewOpacity)));
        }
        if (SetField(ref _appearanceCornerRadius, settings.Appearance.CornerRadius, nameof(AppearanceCornerRadius)))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PreviewCornerRadius)));
        }
        AnimationIndex = settings.Appearance.Animation switch
        {
            LyricAnimation.None => 0,
            LyricAnimation.Slide => 2,
            LyricAnimation.Karaoke => 3,
            _ => 1,
        };
        LyricsTimingOffsetMilliseconds = settings.Lyrics.TimingOffsetMilliseconds;
        FollowCurrentLine = settings.Lyrics.FollowCurrentLine;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
