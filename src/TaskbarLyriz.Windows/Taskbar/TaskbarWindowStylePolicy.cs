namespace TaskbarLyriz.Windows.Taskbar;

internal static class TaskbarWindowStylePolicy
{
    internal const long NoActivate = 0x08000000L;
    internal const long ToolWindow = 0x00000080L;
    internal const long AppWindow = 0x00040000L;
    internal const long Transparent = 0x00000020L;

    internal static long Apply(long currentStyle) =>
        (currentStyle | NoActivate | ToolWindow | Transparent) & ~AppWindow;
}
