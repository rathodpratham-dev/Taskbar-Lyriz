using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using TaskbarLyriz.App.ViewModels;
using TaskbarLyriz.Core.Configuration;

namespace TaskbarLyriz.App.Pages;

public sealed partial class LyricsPage : Page
{
    private bool _isLoaded;

    public LyricsPage()
    {
        ViewModel = App.GetService<LyricsViewModel>();
        InitializeComponent();
    }

    public LyricsViewModel ViewModel { get; }

    private void Page_Loaded(object sender, RoutedEventArgs args)
    {
        _isLoaded = true;
        ViewModel.CurrentLineChanged += OnCurrentLineChanged;
        ScrollToLine(ViewModel.CurrentLineIndex);
    }

    private void Page_Unloaded(object sender, RoutedEventArgs args)
    {
        _isLoaded = false;
        ViewModel.CurrentLineChanged -= OnCurrentLineChanged;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs args) =>
        await ViewModel.RefreshAsync();

    private async void OffsetEarlier_Click(object sender, RoutedEventArgs args) =>
        await ViewModel.AdjustTimingOffsetAsync(-250);

    private async void OffsetReset_Click(object sender, RoutedEventArgs args) =>
        await ViewModel.SetTimingOffsetAsync(0);

    private async void OffsetLater_Click(object sender, RoutedEventArgs args) =>
        await ViewModel.AdjustTimingOffsetAsync(250);

    private async void FollowCurrentLine_Toggled(object sender, RoutedEventArgs args)
    {
        if (_isLoaded && sender is ToggleSwitch toggle)
        {
            await ViewModel.SetFollowCurrentLineAsync(toggle.IsOn);
        }
    }

    private void OnCurrentLineChanged(object? sender, CurrentLyricLineChangedEventArgs args)
    {
        AnimateCurrentLine();
        if (ViewModel.FollowCurrentLine)
        {
            ScrollToLine(args.Index ?? -1);
        }
    }

    private void ScrollToLine(int index)
    {
        if (!_isLoaded || index < 0 || index >= ViewModel.Lines.Count)
        {
            return;
        }

        var line = ViewModel.Lines[index];
        DispatcherQueue.TryEnqueue(() =>
            LyricsList.ScrollIntoView(line, ScrollIntoViewAlignment.Leading));
    }

    private void AnimateCurrentLine()
    {
        if (!_isLoaded || ViewModel.Animation == LyricAnimation.None)
        {
            CurrentLyricText.Opacity = 1;
            CurrentLyricTranslate.Y = 0;
            return;
        }

        var duration = new Duration(TimeSpan.FromMilliseconds(240));
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var storyboard = new Storyboard();
        var fade = new DoubleAnimation
        {
            From = 0.25,
            To = 1,
            Duration = duration,
            EasingFunction = easing,
        };
        Storyboard.SetTarget(fade, CurrentLyricText);
        Storyboard.SetTargetProperty(fade, "Opacity");
        storyboard.Children.Add(fade);

        if (ViewModel.Animation is LyricAnimation.Slide or LyricAnimation.Karaoke)
        {
            var slide = new DoubleAnimation
            {
                From = 8,
                To = 0,
                Duration = duration,
                EasingFunction = easing,
            };
            Storyboard.SetTarget(slide, CurrentLyricTranslate);
            Storyboard.SetTargetProperty(slide, "Y");
            storyboard.Children.Add(slide);
        }

        storyboard.Begin();
    }
}
