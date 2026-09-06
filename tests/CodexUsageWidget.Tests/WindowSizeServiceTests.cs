using CodexUsageWidget.Services;

namespace CodexUsageWidget.Tests;

public sealed class WindowSizeServiceTests
{
    [Fact]
    public void AppliesDragDeltaToBothDimensions()
    {
        var result = WindowSizeService.Resize(220, 66, 70, 22, 220, 66);

        Assert.Equal(new WindowSize(290, 88), result);
    }

    [Fact]
    public void ClampsResizeToMinimumDimensions()
    {
        var result = WindowSizeService.Resize(290, 88, -500, -500, 220, 66);

        Assert.Equal(new WindowSize(220, 66), result);
    }

    [Fact]
    public void RestoresDefaultsWhenSavedDimensionsAreMissing()
    {
        var result = WindowSizeService.Restore(null, null, 330, 98, 330, 98);

        Assert.Equal(new WindowSize(330, 98), result);
    }

    [Fact]
    public void ClampsRestoredDimensionsToMinimums()
    {
        var result = WindowSizeService.Restore(180, 50, 330, 98, 220, 66);

        Assert.Equal(new WindowSize(220, 66), result);
    }
}
