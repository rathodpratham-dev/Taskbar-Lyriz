using Microsoft.UI.Xaml.Controls;
using TaskbarLyriz.App.ViewModels;

namespace TaskbarLyriz.App.Pages;

public sealed partial class PlaybackPage : Page
{
    public PlaybackPage()
    {
        ViewModel = App.GetService<MediaSessionViewModel>();
        InitializeComponent();
    }

    public MediaSessionViewModel ViewModel { get; }
}
