using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskbarLyriz.App.ViewModels;

namespace TaskbarLyriz.App.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _isReady;

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; }

    private async void Page_Loaded(object sender, RoutedEventArgs args)
    {
        _isReady = false;
        await ViewModel.RefreshStartupStatusAsync();
        _isReady = true;
    }

    private async void StartWithWindows_Toggled(object sender, RoutedEventArgs args)
    {
        if (!_isReady || sender is not ToggleSwitch toggle)
        {
            return;
        }

        _isReady = false;
        await ViewModel.SetStartWithWindowsAsync(toggle.IsOn);
        _isReady = true;
    }

    private async void StartMinimized_Toggled(object sender, RoutedEventArgs args)
    {
        if (!_isReady || sender is not ToggleSwitch toggle)
        {
            return;
        }

        await ViewModel.SetStartMinimizedAsync(toggle.IsOn);
    }

    private async void MinimizeToTray_Toggled(object sender, RoutedEventArgs args)
    {
        if (!_isReady || sender is not ToggleSwitch toggle)
        {
            return;
        }

        await ViewModel.SetMinimizeToTrayAsync(toggle.IsOn);
    }

    private async void TaskbarLyrics_Toggled(object sender, RoutedEventArgs args)
    {
        if (!_isReady || sender is not ToggleSwitch toggle)
        {
            return;
        }

        await ViewModel.SetTaskbarLyricsEnabledAsync(toggle.IsOn);
    }

    private async void ThemePicker_SelectionChanged(
        object sender,
        SelectionChangedEventArgs args)
    {
        if (!_isReady || sender is not ComboBox comboBox)
        {
            return;
        }

        await ViewModel.SetThemeAsync(comboBox.SelectedIndex);
    }

    private async void LyricsOffset_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_isReady)
        {
            return;
        }

        await ViewModel.SetLyricsTimingOffsetAsync(args.NewValue);
    }

    private async void FollowCurrentLine_Toggled(object sender, RoutedEventArgs args)
    {
        if (!_isReady || sender is not ToggleSwitch toggle)
        {
            return;
        }

        await ViewModel.SetFollowCurrentLineAsync(toggle.IsOn);
    }
}
