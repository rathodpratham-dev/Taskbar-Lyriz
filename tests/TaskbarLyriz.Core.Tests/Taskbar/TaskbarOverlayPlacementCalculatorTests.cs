using TaskbarLyriz.Core.Configuration;
using TaskbarLyriz.Core.Taskbar;

namespace TaskbarLyriz.Core.Tests.Taskbar;

[TestClass]
public sealed class TaskbarOverlayPlacementCalculatorTests
{
    [TestMethod]
    public void Calculate_RightPosition_AnchorsInsideAvailableArea()
    {
        var result = TaskbarOverlayPlacementCalculator.Calculate(
            new PixelRect(0, 0, 1_920, 1_040),
            420,
            72,
            TaskbarPosition.Right,
            12);

        Assert.AreEqual(new TaskbarOverlayBounds(1_488, 956, 420, 72), result);
    }

    [TestMethod]
    public void Calculate_LeftPosition_SupportsNegativeMonitorCoordinates()
    {
        var result = TaskbarOverlayPlacementCalculator.Calculate(
            new PixelRect(-1_920, 0, 0, 1_040),
            420,
            72,
            TaskbarPosition.Left,
            12);

        Assert.AreEqual(new TaskbarOverlayBounds(-1_908, 956, 420, 72), result);
    }

    [TestMethod]
    public void Calculate_OversizedSurface_ClampsWithinAvailableArea()
    {
        var result = TaskbarOverlayPlacementCalculator.Calculate(
            new PixelRect(100, 50, 400, 250),
            1_000,
            1_000,
            TaskbarPosition.Right,
            20);

        Assert.AreEqual(new TaskbarOverlayBounds(120, 70, 260, 160), result);
    }

    [TestMethod]
    public void Calculate_InvalidArea_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            TaskbarOverlayPlacementCalculator.Calculate(
                new PixelRect(0, 0, 0, 100),
                420,
                72,
                TaskbarPosition.Left,
                12));
    }

    [TestMethod]
    public void TryCalculateInsideTaskbar_Left_UsesGapBetweenWidgetsAndTaskList()
    {
        var result = TaskbarOverlayPlacementCalculator.TryCalculateInsideTaskbar(
            new PixelRect(0, 1_020, 1_920, 1_080),
            new PixelRect(516, 1_020, 1_128, 1_080),
            new PixelRect(1_609, 1_020, 1_920, 1_080),
            desiredWidth: 525,
            minimumWidth: 150,
            desiredHeight: 50,
            TaskbarPosition.Left,
            margin: 8);

        Assert.AreEqual(new TaskbarOverlayBounds(200, 1_025, 308, 50), result);
    }

    [TestMethod]
    public void TryCalculateInsideTaskbar_Right_StaysBeforeNotificationArea()
    {
        var result = TaskbarOverlayPlacementCalculator.TryCalculateInsideTaskbar(
            new PixelRect(0, 1_020, 1_920, 1_080),
            new PixelRect(516, 1_020, 1_128, 1_080),
            new PixelRect(1_609, 1_020, 1_920, 1_080),
            desiredWidth: 525,
            minimumWidth: 150,
            desiredHeight: 50,
            TaskbarPosition.Right,
            margin: 8);

        Assert.AreEqual(new TaskbarOverlayBounds(1_448, 1_025, 153, 50), result);
    }

    [TestMethod]
    public void TryCalculateInsideTaskbar_NoSafeGap_ReturnsNull()
    {
        var result = TaskbarOverlayPlacementCalculator.TryCalculateInsideTaskbar(
            new PixelRect(0, 720, 1_280, 768),
            new PixelRect(120, 720, 1_180, 768),
            new PixelRect(1_180, 720, 1_280, 768),
            desiredWidth: 400,
            minimumWidth: 160,
            desiredHeight: 40,
            TaskbarPosition.Left,
            margin: 8);

        Assert.IsNull(result);
    }
}
