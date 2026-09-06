using CodexUsageWidget.Services;

namespace CodexUsageWidget.Tests;

public sealed class WindowPlacementServiceTests
{
    [Fact]
    public void DefaultsToUpperRightWorkingAreaCorner()
    {
        var result = WindowPlacementService.GetDefaultPosition(0, 0, 1920, 220, 20);

        Assert.Equal(new WindowPosition(1680, 20), result);
    }

    [Fact]
    public void ClampsPositionToVisibleWorkingArea()
    {
        var result = WindowPlacementService.Clamp(-900, 1200, 330, 86, 0, 0, 1920, 1040);

        Assert.Equal(new WindowPosition(0, 954), result);
    }

    [Fact]
    public void PreservesPositionAlreadyInsideWorkingArea()
    {
        var result = WindowPlacementService.Clamp(120, 80, 330, 86, 0, 0, 1920, 1040);

        Assert.Equal(new WindowPosition(120, 80), result);
    }
}
