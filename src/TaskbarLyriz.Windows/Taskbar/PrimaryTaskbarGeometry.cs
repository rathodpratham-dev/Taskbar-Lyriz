using TaskbarLyriz.Core.Taskbar;

namespace TaskbarLyriz.Windows.Taskbar;

public readonly record struct PrimaryTaskbarGeometry(
    PixelRect MonitorArea,
    PixelRect TaskbarArea,
    PixelRect? TaskListArea,
    PixelRect? NotificationArea,
    bool IsBottomTaskbar);

public enum TaskbarWindowShowResult
{
    Shown,
    UnsupportedTaskbarEdge,
    NoSafeSpace,
}
