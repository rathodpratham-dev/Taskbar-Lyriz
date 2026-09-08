using System.ComponentModel;
using System.Runtime.InteropServices;
using TaskbarLyriz.Core.Abstractions;
using TaskbarLyriz.Core.Models;

namespace TaskbarLyriz.Windows.Shell;

public sealed class TrayIconService : ITrayIconService
{
    private const uint WindowMessageTray = 0x8001;
    private const uint WindowMessageNull = 0x0000;
    private const uint WindowMessageContextMenu = 0x007B;
    private const uint WindowMessageLeftButtonDoubleClick = 0x0203;
    private const uint NotifyIconSelect = 0x0400;
    private const uint NotifyIconKeySelect = 0x0401;
    private const uint NotifyIconAdd = 0x00000000;
    private const uint NotifyIconModify = 0x00000001;
    private const uint NotifyIconDelete = 0x00000002;
    private const uint NotifyIconSetFocus = 0x00000003;
    private const uint NotifyIconSetVersion = 0x00000004;
    private const uint NotifyIconFlagMessage = 0x00000001;
    private const uint NotifyIconFlagIcon = 0x00000002;
    private const uint NotifyIconFlagTip = 0x00000004;
    private const uint NotifyIconVersion4 = 4;
    private const uint MenuFlagString = 0x00000000;
    private const uint MenuFlagGray = 0x00000001;
    private const uint MenuFlagSeparator = 0x00000800;
    private const uint TrackPopupRightButton = 0x0002;
    private const uint TrackPopupReturnCommand = 0x0100;
    private const uint TrackPopupNoNotify = 0x0080;
    private const uint ImageIcon = 1;
    private const uint LoadFromFile = 0x0010;
    private const uint LoadDefaultSize = 0x0040;
    private const uint DefaultApplicationIcon = 32512;
    private const int ErrorClassAlreadyExists = 1410;
    private const uint CommandShow = 1001;
    private const uint CommandSettings = 1002;
    private const uint CommandToggleTaskbarLyrics = 1003;
    private const uint CommandExit = 1004;
    private static readonly nint MessageOnlyWindow = new(-3);

    private readonly string _iconPath;
    private readonly IAppLogger _logger;
    private readonly WindowProcedure _windowProcedure;
    private readonly string _windowClassName = $"TaskbarLyriz.Tray.{Guid.NewGuid():N}";
    private TrayIconState _state = new(true);
    private nint _moduleHandle;
    private nint _windowHandle;
    private nint _iconHandle;
    private uint _taskbarCreatedMessage;
    private bool _ownsIcon;
    private bool _initialized;
    private bool _disposed;

    public TrayIconService(string iconPath, IAppLogger logger)
    {
        _iconPath = iconPath;
        _logger = logger;
        _windowProcedure = WindowProc;
    }

    public event EventHandler? ShowRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? TaskbarLyricsToggleRequested;

    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized)
        {
            return;
        }

        _moduleHandle = GetModuleHandle(null);
        var windowClass = new WindowClass
        {
            WindowProcedure = _windowProcedure,
            Instance = _moduleHandle,
            ClassName = _windowClassName,
        };

        if (RegisterClass(ref windowClass) == 0 && Marshal.GetLastWin32Error() != ErrorClassAlreadyExists)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the tray message window.");
        }

        _windowHandle = CreateWindowEx(
            0,
            _windowClassName,
            "TaskbarLyriz Tray",
            0,
            0,
            0,
            0,
            0,
            MessageOnlyWindow,
            nint.Zero,
            _moduleHandle,
            nint.Zero);

        if (_windowHandle == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the tray message window.");
        }

        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
        LoadTrayIcon();
        AddIcon();
        _initialized = true;
        _logger.Information("tray.initialized", "The notification-area icon was initialized.");
    }

    public void Update(TrayIconState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;

        if (!_initialized || _disposed)
        {
            return;
        }

        var data = CreateNotifyIconData();
        data.Flags = NotifyIconFlagTip;
        ShellNotifyIcon(NotifyIconModify, ref data);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_windowHandle != nint.Zero)
        {
            var data = CreateNotifyIconData();
            ShellNotifyIcon(NotifyIconDelete, ref data);
            DestroyWindow(_windowHandle);
            _windowHandle = nint.Zero;
        }

        if (_ownsIcon && _iconHandle != nint.Zero)
        {
            DestroyIcon(_iconHandle);
        }

        _iconHandle = nint.Zero;
        UnregisterClass(_windowClassName, _moduleHandle);
    }

    private nint WindowProc(nint windowHandle, uint message, nint wParam, nint lParam)
    {
        if (message == _taskbarCreatedMessage && _initialized)
        {
            AddIcon();
            return nint.Zero;
        }

        if (message == WindowMessageTray)
        {
            var notification = TrayMessageDecoder.GetNotificationCode(lParam);
            switch (notification)
            {
                case NotifyIconSelect:
                case NotifyIconKeySelect:
                case WindowMessageLeftButtonDoubleClick:
                    ShowRequested?.Invoke(this, EventArgs.Empty);
                    return nint.Zero;
                case WindowMessageContextMenu:
                    ShowContextMenu();
                    return nint.Zero;
            }
        }

        return DefWindowProc(windowHandle, message, wParam, lParam);
    }

    private void LoadTrayIcon()
    {
        if (File.Exists(_iconPath))
        {
            _iconHandle = LoadImage(
                nint.Zero,
                _iconPath,
                ImageIcon,
                0,
                0,
                LoadFromFile | LoadDefaultSize);
            _ownsIcon = _iconHandle != nint.Zero;
        }

        if (_iconHandle == nint.Zero)
        {
            _iconHandle = LoadIcon(nint.Zero, new nint(DefaultApplicationIcon));
            _ownsIcon = false;
        }
    }

    private void AddIcon()
    {
        var data = CreateNotifyIconData();
        data.Flags = NotifyIconFlagMessage | NotifyIconFlagIcon | NotifyIconFlagTip;
        if (!ShellNotifyIcon(NotifyIconAdd, ref data))
        {
            _logger.Warning("tray.add_failed", "Windows did not add the notification-area icon.");
            return;
        }

        data.Version = NotifyIconVersion4;
        ShellNotifyIcon(NotifyIconSetVersion, ref data);
    }

    private NotifyIconData CreateNotifyIconData() => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(),
        WindowHandle = _windowHandle,
        Id = 1,
        CallbackMessage = WindowMessageTray,
        IconHandle = _iconHandle,
        ToolTip = BuildToolTip(),
    };

    private string BuildToolTip()
    {
        var song = string.IsNullOrWhiteSpace(_state.CurrentSong) ? "Nothing playing" : _state.CurrentSong;
        var tooltip = $"TaskbarLyriz \u2014 {song}";
        return tooltip.Length <= 127 ? tooltip : tooltip[..127];
    }

    private void ShowContextMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == nint.Zero)
        {
            return;
        }

        try
        {
            AppendMenu(menu, MenuFlagString, CommandShow, "Show TaskbarLyriz");
            AppendMenu(menu, MenuFlagString | MenuFlagGray, 0, _state.CurrentSong);
            AppendMenu(menu, MenuFlagSeparator, 0, null);
            AppendMenu(
                menu,
                MenuFlagString,
                CommandToggleTaskbarLyrics,
                _state.TaskbarLyricsEnabled ? "Hide Taskbar Lyrics" : "Show Taskbar Lyrics");
            AppendMenu(menu, MenuFlagString, CommandSettings, "Settings");
            AppendMenu(menu, MenuFlagSeparator, 0, null);
            AppendMenu(menu, MenuFlagString, CommandExit, "Exit");

            GetCursorPos(out var cursor);
            SetForegroundWindow(_windowHandle);
            var command = TrackPopupMenu(
                menu,
                TrackPopupRightButton | TrackPopupReturnCommand | TrackPopupNoNotify,
                cursor.X,
                cursor.Y,
                0,
                _windowHandle,
                nint.Zero);

            DispatchCommand(command);
            PostMessage(_windowHandle, WindowMessageNull, nint.Zero, nint.Zero);
            var data = CreateNotifyIconData();
            ShellNotifyIcon(NotifyIconSetFocus, ref data);
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private void DispatchCommand(uint command)
    {
        switch (command)
        {
            case CommandShow:
                ShowRequested?.Invoke(this, EventArgs.Empty);
                break;
            case CommandSettings:
                SettingsRequested?.Invoke(this, EventArgs.Empty);
                break;
            case CommandToggleTaskbarLyrics:
                TaskbarLyricsToggleRequested?.Invoke(this, EventArgs.Empty);
                break;
            case CommandExit:
                ExitRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowProcedure(nint windowHandle, uint message, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        internal uint Style;
        internal WindowProcedure WindowProcedure;
        internal int ClassExtraBytes;
        internal int WindowExtraBytes;
        internal nint Instance;
        internal nint Icon;
        internal nint Cursor;
        internal nint BackgroundBrush;
        [MarshalAs(UnmanagedType.LPWStr)] internal string? MenuName;
        [MarshalAs(UnmanagedType.LPWStr)] internal string ClassName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        internal uint Size;
        internal nint WindowHandle;
        internal uint Id;
        internal uint Flags;
        internal uint CallbackMessage;
        internal nint IconHandle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string ToolTip;
        internal uint State;
        internal uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] internal string? Info;
        internal uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] internal string? InfoTitle;
        internal uint InfoFlags;
        internal Guid ItemGuid;
        internal nint BalloonIconHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        internal int X;
        internal int Y;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClass(ref WindowClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool UnregisterClass(string className, nint instance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(nint windowHandle, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(nint windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string messageName);

    [DllImport(
        "shell32.dll",
        EntryPoint = "Shell_NotifyIconW",
        CharSet = CharSet.Unicode,
        ExactSpelling = true,
        SetLastError = true)]
    private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadImage(
        nint instance,
        string name,
        uint type,
        int desiredWidth,
        int desiredHeight,
        uint loadFlags);

    [DllImport("user32.dll")]
    private static extern nint LoadIcon(nint instance, nint iconName);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint iconHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool AppendMenu(nint menu, uint flags, uint itemId, string? itemText);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(nint menu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint TrackPopupMenu(
        nint menu,
        uint flags,
        int x,
        int y,
        int reserved,
        nint windowHandle,
        nint rectangle);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(nint windowHandle, uint message, nint wParam, nint lParam);
}
