using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TaskbarLyriz.App.ViewModels;

public sealed class LyricDisplayLine(string timestamp, string text, double baseFontSize = 15) : INotifyPropertyChanged
{
    private double _opacity = 1;
    private double _baseFontSize = baseFontSize;
    private double _fontSize = baseFontSize + 2;
    private bool _isCurrent;
    private bool _isAdjacent;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Timestamp { get; } = timestamp;

    public string Text { get; } = text;

    public double Opacity
    {
        get => _opacity;
        private set => SetField(ref _opacity, value);
    }

    public double FontSize
    {
        get => _fontSize;
        private set => SetField(ref _fontSize, value);
    }

    public bool IsCurrent
    {
        get => _isCurrent;
        private set => SetField(ref _isCurrent, value);
    }

    public void SetPosition(bool isCurrent, bool isAdjacent)
    {
        IsCurrent = isCurrent;
        _isAdjacent = isAdjacent;
        Opacity = isCurrent ? 1 : isAdjacent ? 0.78 : 0.58;
        ApplyFontSize();
    }

    public void ApplyTypography(double baseFontSize)
    {
        _baseFontSize = Math.Clamp(baseFontSize, 10, 36);
        ApplyFontSize();
    }

    private void ApplyFontSize()
    {
        FontSize = IsCurrent ? _baseFontSize + 6 : _isAdjacent ? _baseFontSize + 3 : _baseFontSize + 1;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
