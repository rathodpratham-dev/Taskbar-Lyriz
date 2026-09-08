namespace TaskbarLyriz.Core.Taskbar;

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Math.Max(0, Right - Left);

    public int Height => Math.Max(0, Bottom - Top);
}
