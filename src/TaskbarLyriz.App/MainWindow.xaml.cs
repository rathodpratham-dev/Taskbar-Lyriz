using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskbarLyriz.App.Pages;
using TaskbarLyriz.Core.Abstractions;
using TaskbarLyriz.Core.Configuration;
using Windows.Graphics;

namespace TaskbarLyriz.App;

public sealed partial class MainWindow : Window
{
    private readonly IAppSettingsService _settings;
    private readonly IAppLogger _logger;
    private bool _allowClose;

    public MainWindow(IAppSettingsService settings, IAppLogger logger)
    {
        _settings = settings;
        _logger = logger;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new SizeInt32(1_120, 720));
        AppWindow.Closing += OnAppWindowClosing;
        _settings.Changed += OnSettingsChanged;

        ApplyTheme(_settings.Current.Appearance.Theme);
        NavFrame.Navigate(typeof(HomePage));
    }

    public event EventHandler? ExitRequested;

    public void ShowAndActivate()
    {
        AppWindow.Show();
        Activate();
    }

    public void HideToTray()
    {
        AppWindow.Hide();
        _logger.Information("window.hidden", "The main window was hidden to the notification area.");
    }

    public void NavigateToSettings()
    {
        NavView.SelectedItem = NavView.SettingsItem;
        NavigateIfNeeded(typeof(SettingsPage));
    }

    public void AllowCloseAndClose()
    {
        _allowClose = true;
        _settings.Changed -= OnSettingsChanged;
        Close();
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        if (NavFrame.CanGoBack)
        {
            NavFrame.GoBack();
        }
    }

    private void NavView_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavigateIfNeeded(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

        switch (tag)
        {
            case "home":
                NavigateIfNeeded(typeof(HomePage));
                break;
            case "about":
                NavigateIfNeeded(typeof(AboutPage));
                break;
            case "lyrics":
                NavigateIfNeeded(typeof(LyricsPage));
                break;
            case "taskbar":
                NavigateIfNeeded(typeof(TaskbarPage));
                break;
            case "appearance":
                NavigateIfNeeded(typeof(AppearancePage));
                break;
            case "playback":
                NavigateIfNeeded(typeof(PlaybackPage));
                break;
            case "providers":
                NavigateIfNeeded(typeof(ProvidersPage));
                break;
            case "cache":
                NavigateIfNeeded(typeof(CachePage));
                break;
        }
    }

    private void NavigateIfNeeded(Type pageType)
    {
        if (NavFrame.CurrentSourcePageType != pageType)
        {
            NavFrame.Navigate(pageType);
        }
    }

    private void NavigateFeature(FeaturePageDescriptor descriptor)
    {
        NavFrame.Navigate(typeof(FeaturePage), descriptor);
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        args.Cancel = true;
        if (_settings.Current.General.MinimizeToTray)
        {
            HideToTray();
            return;
        }

        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        DispatcherQueue.TryEnqueue(() => ApplyTheme(settings.Appearance.Theme));
    }

    private void ApplyTheme(AppTheme theme)
    {
        RootShell.RequestedTheme = theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }
}
