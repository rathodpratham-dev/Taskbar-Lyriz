using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace TaskbarLyriz.App.Pages;

public sealed partial class FeaturePage : Page
{
    public FeaturePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (args.Parameter is not FeaturePageDescriptor feature)
        {
            return;
        }

        FeatureIcon.Glyph = feature.Glyph;
        TitleText.Text = feature.Title;
        DescriptionText.Text = feature.Description;
        MilestoneInfo.Message = $"Scheduled for {feature.Milestone}.";
        ScopeText.Text = feature.Scope;
    }
}
