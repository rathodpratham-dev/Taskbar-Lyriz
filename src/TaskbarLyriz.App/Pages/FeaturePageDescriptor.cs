namespace TaskbarLyriz.App.Pages;

public sealed record FeaturePageDescriptor(
    string Title,
    string Description,
    string Milestone,
    string Glyph,
    string Scope)
{
    public static FeaturePageDescriptor Taskbar { get; } = new(
        "Taskbar",
        "Configure the safe, non-activating lyric surface that sits alongside the Windows taskbar.",
        "Milestone 5",
        "\uE7F4",
        "Position, display mode, monitor selection, width, and line count.");

}
