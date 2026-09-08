using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using TaskbarLyriz.Core.Configuration;
using TaskbarLyriz.Core.Taskbar;

namespace TaskbarLyriz.Windows.Taskbar;

public sealed class TaskbarWindowController
{
    private const int ExtendedStyleIndex = -20;
    private const int WindowProcedureIndex = -4;
    private const uint MonitorDefaultToPrimary = 1;
    private const uint AppBarGetTaskbarPosition = 5;
    private const uint AppBarEdgeBottom = 3;
    private const uint SetWindowPositionNoActivate = 0x0010;
    private const uint SetWindowPositionShowWindow = 0x0040;
    private const int ShowWindowHide = 0;
    private const int ShowWindowNoActivate = 4;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmWindowCornerRoundSmall = 3;
    private const uint WindowMessageMouseActivate = 0x0021;
    private const uint WindowMessageNonClientHitTest = 0x0084;
    private const int MouseActivateNoActivate = 3;
    private static readonly nint TopMostWindow = new(-1);
    private WindowProcedure? _windowProcedure;
    private nint _originalWindowProcedure;

    public void Configure(nint windowHandle)
    {
        ArgumentOutOfRangeException.ThrowIfZero(windowHandle);

        var currentStyle = GetExtendedStyle(windowHandle);
        var nextStyle = TaskbarWindowStylePolicy.Apply(currentStyle);
        if (nextStyle != currentStyle)
        {
            var previousStyle = SetExtendedStyle(windowHandle, nextStyle);
            if (previousStyle == 0 && Marshal.GetLastWin32Error() != 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        var cornerPreference = DwmWindowCornerRoundSmall;
        _ = DwmSetWindowAttribute(
            windowHandle,
            DwmWindowCornerPreference,
            ref cornerPreference,
            sizeof(int));

        _windowProcedure = WindowProc;
        _originalWindowProcedure = SetWindowProcedure(windowHandle, _windowProcedure);
        if (_originalWindowProcedure == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public TaskbarWindowShowResult PositionAndShow(
        nint windowHandle,
        TaskbarPosition position,
        int widthInViewPixels,
        int heightInViewPixels,
        int marginInViewPixels = 6)
    {
        ArgumentOutOfRangeException.ThrowIfZero(windowHandle);

        var geometry = ReadPrimaryTaskbarGeometry();
        if (!geometry.IsBottomTaskbar)
        {
            return TaskbarWindowShowResult.UnsupportedTaskbarEdge;
        }

        var scale = Math.Clamp(GetDpiForWindow(windowHandle) / 96d, 0.5d, 8d);
        var bounds = TaskbarOverlayPlacementCalculator.TryCalculateInsideTaskbar(
            geometry.TaskbarArea,
            geometry.TaskListArea,
            geometry.NotificationArea,
            Scale(widthInViewPixels, scale),
            Scale(120, scale),
            Scale(heightInViewPixels, scale),
            position,
            Scale(marginInViewPixels, scale));
        if (bounds is null)
        {
            return TaskbarWindowShowResult.NoSafeSpace;
        }

        if (!SetWindowPos(
                windowHandle,
                TopMostWindow,
                bounds.Value.X,
                bounds.Value.Y,
                bounds.Value.Width,
                bounds.Value.Height,
                SetWindowPositionNoActivate | SetWindowPositionShowWindow))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        _ = ShowWindow(windowHandle, ShowWindowNoActivate);
        return TaskbarWindowShowResult.Shown;
    }

    public void Hide(nint windowHandle)
    {
        if (windowHandle != 0)
        {
            _ = ShowWindow(windowHandle, ShowWindowHide);
        }
    }

    public void Release(nint windowHandle)
    {
        Hide(windowHandle);
        if (windowHandle != 0 && _originalWindowProcedure != 0)
        {
            _ = SetWindowProcedure(windowHandle, _originalWindowProcedure);
            _originalWindowProcedure = 0;
            _windowProcedure = null;
        }
    }

    public PrimaryTaskbarGeometry ReadPrimaryTaskbarGeometry()
    {
        var monitorHandle = MonitorFromPoint(default, MonitorDefaultToPrimary);
        if (monitorHandle == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var monitorInfo = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitorHandle, ref monitorInfo))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var monitorArea = monitorInfo.Monitor.ToPixelRect();
        var appBarData = new AppBarData { Size = (uint)Marshal.SizeOf<AppBarData>() };
        var hasTaskbarPosition = SHAppBarMessage(AppBarGetTaskbarPosition, ref appBarData) != 0;
        var isBottomTaskbar = hasTaskbarPosition
            ? appBarData.Edge == AppBarEdgeBottom && Intersects(appBarData.Rectangle, monitorInfo.Monitor)
            : monitorInfo.WorkArea.Bottom < monitorInfo.Monitor.Bottom;

        var taskbarWindow = FindWindow("Shell_TrayWnd", null);
        var taskbarArea = hasTaskbarPosition
            ? appBarData.Rectangle.ToPixelRect()
            : TryReadWindowRect(taskbarWindow) ?? monitorInfo.WorkArea.ToPixelRect();
        var taskListWindow = FindDescendantByClass(taskbarWindow, "MSTaskSwWClass");
        if (taskListWindow == 0)
        {
            taskListWindow = FindDescendantByClass(taskbarWindow, "MSTaskListWClass");
        }

        var notificationWindow = FindDescendantByClass(taskbarWindow, "TrayNotifyWnd");
        return new PrimaryTaskbarGeometry(
            monitorArea,
            taskbarArea,
            TryReadWindowRect(taskListWindow),
            TryReadWindowRect(notificationWindow),
            isBottomTaskbar);
    }

    private static int Scale(int value, double scale) =>
        Math.Max(1, (int)Math.Round(value * scale, MidpointRounding.AwayFromZero));

    private static bool Intersects(NativeRect first, NativeRect second) =>
        first.Left < second.Right && first.Right > second.Left &&
        first.Top < second.Bottom && first.Bottom > second.Top;

    private static nint FindDescendantByClass(nint parentWindow, string className)
    {
        if (parentWindow == 0)
        {
            return 0;
        }

        nint match = 0;
        _ = EnumChildWindows(parentWindow, (windowHandle, _) =>
        {
            var buffer = new StringBuilder(256);
            _ = GetClassName(windowHandle, buffer, buffer.Capacity);
            if (!string.Equals(buffer.ToString(), className, StringComparison.Ordinal))
            {
                return true;
            }

            match = windowHandle;
            return false;
        }, 0);
        return match;
    }

    private static PixelRect? TryReadWindowRect(nint windowHandle)
    {
        if (windowHandle == 0 || !GetWindowRect(windowHandle, out var rectangle))
        {
            return null;
        }

        return rectangle.ToPixelRect();
    }

    private static long GetExtendedStyle(nint windowHandle) => IntPtr.Size == 8
        ? GetWindowLongPtr64(windowHandle, ExtendedStyleIndex).ToInt64()
        : GetWindowLong32(windowHandle, ExtendedStyleIndex);

    private static long SetExtendedStyle(nint windowHandle, long style) => IntPtr.Size == 8
        ? SetWindowLongPtr64(windowHandle, ExtendedStyleIndex, new nint(style)).ToInt64()
        : SetWindowLong32(windowHandle, ExtendedStyleIndex, (int)style);

    private static nint SetWindowProcedure(nint windowHandle, WindowProcedure procedure) =>
        SetWindowProcedure(windowHandle, Marshal.GetFunctionPointerForDelegate(procedure));

    private static nint SetWindowProcedure(nint windowHandle, nint procedure) => IntPtr.Size == 8
        ? SetWindowLongPtr64(windowHandle, WindowProcedureIndex, procedure)
        : new nint(SetWindowLong32(windowHandle, WindowProcedureIndex, procedure.ToInt32()));

    private nint WindowProc(nint windowHandle, uint message, nint wParam, nint lParam)
    {
        if (message == WindowMessageNonClientHitTest)
        {
            return new nint(-1);
        }

        if (message == WindowMessageMouseActivate)
        {
            return new nint(MouseActivateNoActivate);
        }

        return CallWindowProc(_originalWindowProcedure, windowHandle, message, wParam, lParam);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowProcedure(nint windowHandle, uint message, nint wParam, nint lParam);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool EnumChildProcedure(nint windowHandle, nint parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;

        internal readonly PixelRect ToPixelRect() => new(Left, Top, Right, Bottom);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        internal uint Size;
        internal NativeRect Monitor;
        internal NativeRect WorkArea;
        internal uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        internal uint Size;
        internal nint WindowHandle;
        internal uint CallbackMessage;
        internal uint Edge;
        internal NativeRect Rectangle;
        internal nint Parameter;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr64(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(nint windowHandle, int index, int newLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr64(nint windowHandle, int index, nint newLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(
        nint parentWindow,
        EnumChildProcedure callback,
        nint parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint windowHandle, StringBuilder className, int maximumCount);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint windowHandle, out NativeRect rectangle);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetMonitorInfo(nint monitorHandle, ref MonitorInfo monitorInfo);

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern nuint SHAppBarMessage(uint message, ref AppBarData data);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        nint windowHandle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint windowHandle, int command);

    [DllImport("user32.dll")]
    private static extern nint CallWindowProc(
        nint previousWindowProcedure,
        nint windowHandle,
        uint message,
        nint wParam,
        nint lParam);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref int value,
        int valueSize);
}
