using TaskbarLyriz.Core.Configuration;

namespace TaskbarLyriz.Core.Taskbar;

public static class TaskbarOverlayPlacementCalculator
{
    public static TaskbarOverlayBounds? TryCalculateInsideTaskbar(
        PixelRect taskbarArea,
        PixelRect? taskListArea,
        PixelRect? notificationArea,
        int desiredWidth,
        int minimumWidth,
        int desiredHeight,
        TaskbarPosition position,
        int margin)
    {
        if (taskbarArea.Width <= 0 || taskbarArea.Height <= 0)
        {
            return null;
        }

        margin = Math.Clamp(margin, 0, taskbarArea.Height / 3);
        desiredHeight = Math.Clamp(desiredHeight, 1, taskbarArea.Height);

        var center = taskbarArea.Left + (taskbarArea.Width / 2);
        var centralHalfWidth = Math.Min(taskbarArea.Width / 4, taskbarArea.Height * 9);
        var reservedLeftWidth = Math.Max(taskbarArea.Width / 10, taskbarArea.Height * 3);
        var leftGapStart = taskbarArea.Left + reservedLeftWidth;
        var leftGapEnd = taskListArea is { } taskList
            ? taskList.Left
            : center - centralHalfWidth;
        var rightGapStart = taskListArea is { } taskListForRight
            ? Math.Max(taskListForRight.Right, center + centralHalfWidth)
            : center + centralHalfWidth;
        var rightGapEnd = notificationArea?.Left ??
            taskbarArea.Right - reservedLeftWidth;

        var gapStart = position == TaskbarPosition.Left ? leftGapStart : rightGapStart;
        var gapEnd = position == TaskbarPosition.Left ? leftGapEnd : rightGapEnd;
        gapStart = Math.Max(taskbarArea.Left, gapStart) + margin;
        gapEnd = Math.Min(taskbarArea.Right, gapEnd) - margin;
        var availableWidth = gapEnd - gapStart;
        if (availableWidth < minimumWidth)
        {
            return null;
        }

        var width = Math.Clamp(desiredWidth, minimumWidth, availableWidth);
        var x = gapStart + ((availableWidth - width) / 2);
        var y = taskbarArea.Top + ((taskbarArea.Height - desiredHeight) / 2);
        return new TaskbarOverlayBounds(x, y, width, desiredHeight);
    }

    public static TaskbarOverlayBounds Calculate(
        PixelRect availableArea,
        int desiredWidth,
        int desiredHeight,
        TaskbarPosition position,
        int margin)
    {
        if (availableArea.Width <= 0 || availableArea.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(availableArea),
                "The available desktop area must have a positive width and height.");
        }

        margin = Math.Clamp(
            margin,
            0,
            Math.Min(availableArea.Width / 2, availableArea.Height / 2));
        var usableWidth = Math.Max(1, availableArea.Width - (margin * 2));
        var usableHeight = Math.Max(1, availableArea.Height - (margin * 2));
        var width = Math.Clamp(desiredWidth, 1, usableWidth);
        var height = Math.Clamp(desiredHeight, 1, usableHeight);
        var x = position == TaskbarPosition.Left
            ? availableArea.Left + margin
            : availableArea.Right - margin - width;
        var y = availableArea.Bottom - margin - height;

        return new TaskbarOverlayBounds(x, y, width, height);
    }
}
