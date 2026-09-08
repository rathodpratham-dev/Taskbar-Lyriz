using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskbarLyriz.App.ViewModels;

namespace TaskbarLyriz.App.Pages;

public sealed partial class TaskbarPage : Page
{
    private bool _isReady;

    public TaskbarPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; }

    private void Page_Loaded(object sender, RoutedEventArgs args)
    {
        _isReady = true;
    }

    private async void TaskbarLyrics_Toggled(object sender, RoutedEventArgs args)
    {
        if (_isReady && sender is ToggleSwitch toggle)
        {
            await ViewModel.SetTaskbarLyricsEnabledAsync(toggle.IsOn);
        }
    }

    private async void PositionPicker_SelectionChanged(
        object sender,
        SelectionChangedEventArgs args)
    {
        if (_isReady && sender is ComboBox comboBox)
        {
            await ViewModel.SetTaskbarPositionAsync(comboBox.SelectedIndex);
        }
    }

    private async void WidthBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isReady)
        {
            await ViewModel.SetTaskbarWidthAsync(args.NewValue);
        }
    }
}
