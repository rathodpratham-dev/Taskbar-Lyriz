using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using TaskbarLyriz.App.ViewModels;

namespace TaskbarLyriz.App.Pages;

public sealed partial class AppearancePage : Page
{
    private bool _isReady;

    public AppearancePage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; }

    private void Page_Loaded(object sender, RoutedEventArgs args)
    {
        _isReady = true;
    }

    private async void ThemePicker_SelectionChanged(
        object sender,
        SelectionChangedEventArgs args)
    {
        if (_isReady && sender is ComboBox comboBox)
        {
            await ViewModel.SetThemeAsync(comboBox.SelectedIndex);
        }
    }

    private async void FontPicker_SelectionChanged(
        object sender,
        SelectionChangedEventArgs args)
    {
        if (_isReady && sender is ComboBox comboBox)
        {
            await ViewModel.SetFontFamilyAsync(comboBox.SelectedIndex);
        }
    }

    private async void FontSizeBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isReady)
        {
            await ViewModel.SetLyricFontSizeAsync(args.NewValue);
        }
    }

    private async void OpacitySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs args)
    {
        if (_isReady)
        {
            await ViewModel.SetAppearanceOpacityAsync(args.NewValue);
        }
    }

    private async void CornerRadiusBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isReady)
        {
            await ViewModel.SetAppearanceCornerRadiusAsync(args.NewValue);
        }
    }

    private async void AnimationPicker_SelectionChanged(
        object sender,
        SelectionChangedEventArgs args)
    {
        if (_isReady && sender is ComboBox comboBox)
        {
            await ViewModel.SetAnimationAsync(comboBox.SelectedIndex);
        }
    }
}
