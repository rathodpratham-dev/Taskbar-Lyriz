namespace TaskbarLyriz.Windows.Shell;

internal static class TrayMessageDecoder
{
    internal static uint GetNotificationCode(nint parameter) =>
        unchecked((uint)((long)parameter & 0xFFFF));
}
