using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Wagenheimer.MonoGameHelper.Graphics;
using Xunit;

namespace Wagenheimer.MonoGameHelper.Tests;

public class TextureAtlasTests
{
    private const string SampleAtlas = @"
menu.png
size: 2048, 2048
format: RGBA8888
filter: Linear, Linear
repeat: none
scale: 2

btStart
  rotate: false
  xy: 10, 20
  size: 215, 75
  orig: 215, 75
  offset: 0, 0
  index: -1

btQuit
  rotate: false
  xy: 300, 450
  size: 150, 60
  orig: 160, 65
  offset: 5, 2
  index: 1
";

    [Fact]
    public void TestParseAtlasHeaderAndRegions()
    {
        var atlas = TextureAtlas.Parse(SampleAtlas);

        Assert.Equal(2f, atlas.ArtScale.Scale);
        Assert.Equal(0.5f, atlas.ArtScale.BaseScale);
        Assert.Equal(2, atlas.Regions.Count);

        Assert.True(atlas.ContainsRegion("btStart"));
        Assert.True(atlas.ContainsRegion("btQuit"));
        Assert.False(atlas.ContainsRegion("missing_sprite"));

        var start = atlas["btStart"];
        Assert.Equal("btStart", start.Name);
        Assert.Equal(new Rectangle(10, 20, 215, 75), start.Bounds);
        Assert.Equal(new Point(215, 75), start.OriginalSize);
        Assert.Equal(new Point(0, 0), start.Offset);
        Assert.Equal(-1, start.Index);
        Assert.False(start.Rotate);

        var quit = atlas["btQuit"];
        Assert.Equal("btQuit", quit.Name);
        Assert.Equal(new Rectangle(300, 450, 150, 60), quit.Bounds);
        Assert.Equal(new Point(160, 65), quit.OriginalSize);
        Assert.Equal(new Point(5, 2), quit.Offset);
        Assert.Equal(1, quit.Index);
    }

    [Fact]
    public void TestTryGetRegionCaseInsensitive()
    {
        var atlas = TextureAtlas.Parse(SampleAtlas);

        Assert.True(atlas.TryGetRegion("BTSTART", out var region));
        Assert.NotNull(region);
        Assert.Equal(215, region.Bounds.Width);

        Assert.False(atlas.TryGetRegion("non_existent", out var missing));
        Assert.Null(missing);
    }

    [Fact]
    public void TestMissingRegionThrowsKeyNotFoundException()
    {
        var atlas = TextureAtlas.Parse(SampleAtlas);
        Assert.Throws<KeyNotFoundException>(() => atlas["does_not_exist"]);
    }
}
