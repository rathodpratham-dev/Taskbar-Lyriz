using Microsoft.UI.Xaml.Controls;
using TaskbarLyriz.App.ViewModels;

namespace TaskbarLyriz.App.Pages;

public sealed partial class HomePage : Page
{
    public HomePage()
    {
        ViewModel = App.GetService<MediaSessionViewModel>();
        Lyrics = App.GetService<LyricsViewModel>();
        InitializeComponent();
    }

    public MediaSessionViewModel ViewModel { get; }

    public LyricsViewModel Lyrics { get; }
}
