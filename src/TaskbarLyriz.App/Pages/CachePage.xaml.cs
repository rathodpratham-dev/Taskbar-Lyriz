using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskbarLyriz.App.ViewModels;

namespace TaskbarLyriz.App.Pages;

public sealed partial class CachePage : Page
{
    public CachePage()
    {
        ViewModel = App.GetService<CacheViewModel>();
        InitializeComponent();
    }

    public CacheViewModel ViewModel { get; }

    private async void Page_Loaded(object sender, RoutedEventArgs args) =>
        await ViewModel.RefreshAsync();

    private async void Refresh_Click(object sender, RoutedEventArgs args) =>
        await ViewModel.RefreshAsync();

    private async void Clear_Click(object sender, RoutedEventArgs args)
    {
        var dialog = new ContentDialog
        {
            Title = "Clear downloaded lyrics?",
            Content = "Saved lyrics will be removed from the SQLite cache and downloaded again when needed.",
            PrimaryButtonText = "Clear",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.ClearAsync();
        }
    }
}
