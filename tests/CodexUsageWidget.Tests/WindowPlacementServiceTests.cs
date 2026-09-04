using CodexUsageWidget.Services;

namespace CodexUsageWidget.Tests;

public sealed class WindowPlacementServiceTests
{
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
