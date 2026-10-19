using PhaseDrop.App.UI.RadialMenu;
using PhaseDrop.Core.Models;
using Xunit;

namespace PhaseDrop.IntegrationTests;

public class RadialPetalTests
{
    [Fact]
    public void RadialPetal_HitTest_AccuratelyDetectsPointsInSector()
    {
        // 4 petals: Petal 0 is from 0 to PI/2 (First Quadrant: x > cx, y > cy)
        var op = new FileOperation("convert:png", "PNG", ".png", OperationCategory.Image);
        var petal = new RadialPetal(op, 0, Math.PI / 2.0)
        {
            InnerRadius = 50,
            OuterRadius = 150
        };

        double cx = 200;
        double cy = 200;

        // Point at r = 100, angle = 45 deg (PI/4) -> should HIT
        double hitX = cx + (100 * Math.Cos(Math.PI / 4.0));
        double hitY = cy + (100 * Math.Sin(Math.PI / 4.0));
        Assert.True(petal.HitTest(hitX, hitY, cx, cy));

        // Point inside inner radius (r = 30) -> should MISS (center hub)
        double innerX = cx + (30 * Math.Cos(Math.PI / 4.0));
        double innerY = cy + (30 * Math.Sin(Math.PI / 4.0));
        Assert.False(petal.HitTest(innerX, innerY, cx, cy));

        // Point outside outer radius (r = 180) -> should MISS
        double outerX = cx + (180 * Math.Cos(Math.PI / 4.0));
        double outerY = cy + (180 * Math.Sin(Math.PI / 4.0));
        Assert.False(petal.HitTest(outerX, outerY, cx, cy));

        // Point in opposite quadrant (angle = PI) -> should MISS
        double oppX = cx - 100;
        double oppY = cy;
        Assert.False(petal.HitTest(oppX, oppY, cx, cy));
    }

    [Fact]
    public void RadialPetal_CreateGeometry_ProducesValidWpfGeometry()
    {
        var op = new FileOperation("convert:webp", "WebP", ".webp", OperationCategory.Image);
        var petal = new RadialPetal(op, 0, Math.PI / 3.0)
        {
            InnerRadius = 60,
            OuterRadius = 160
        };

        var geom = petal.CreateGeometry(200, 200);
        Assert.NotNull(geom);
        Assert.False(geom.IsEmpty());
        Assert.True(geom.Bounds.Width > 0);
        Assert.True(geom.Bounds.Height > 0);
    }
}
