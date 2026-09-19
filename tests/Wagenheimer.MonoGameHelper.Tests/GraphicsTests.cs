using Microsoft.Xna.Framework;
using Wagenheimer.MonoGameHelper.Graphics;
using Xunit;

namespace Wagenheimer.MonoGameHelper.Tests;

public class GraphicsTests
{
    [Fact]
    public void TestFitHeightWiderAspectRatio()
    {
        // Source is 2560x1080 (ultrawide ~21:9), virtual viewport is 1920x1080 (16:9)
        // Scaled height is 1080, scaled width is 2560.
        // X offset should be (1920 - 2560) / 2 = -320
        Rectangle rect = FitHeightBackground.CalculateFitHeight(2560, 1080, 1920, 1080);

        Assert.Equal(1080, rect.Height);
        Assert.Equal(2560, rect.Width);
        Assert.Equal(-320, rect.X);
        Assert.Equal(0, rect.Y);
    }

    [Fact]
    public void TestFitHeightNarrowerAspectRatio()
    {
        // Source is 1920x1080 (16:9), virtual viewport is 3840x1080 (32:9 super ultrawide)
        // Scaled width (1920) is less than virtual width (3840), so it scales to fit width to avoid black bars.
        // scale = 3840 / 1920 = 2.0. Height becomes 2160.
        // Y offset should be (1080 - 2160) / 2 = -540
        Rectangle rect = FitHeightBackground.CalculateFitHeight(1920, 1080, 3840, 1080);

        Assert.Equal(3840, rect.Width);
        Assert.Equal(2160, rect.Height);
        Assert.Equal(0, rect.X);
        Assert.Equal(-540, rect.Y);
    }
}
