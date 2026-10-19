using PhaseDrop.Core.Models;
using PhaseDrop.Core.Registry;

namespace PhaseDrop.Core.Tests;

public class CapabilityRegistryTests
{
    [Theory]
    [InlineData("photo.png", OperationCategory.Image)]
    [InlineData("clip.mp4", OperationCategory.Video)]
    [InlineData("song.mp3", OperationCategory.Audio)]
    [InlineData("doc.pdf", OperationCategory.Pdf)]
    [InlineData("backup.zip", OperationCategory.Archive)]
    [InlineData("data.csv", OperationCategory.Document)]
    [InlineData("notes.md", OperationCategory.Document)]
    public void GetCategory_IdentifiesCorrectCategory(string filename, OperationCategory expected)
    {
        var category = CapabilityRegistry.GetCategory(filename);
        Assert.Equal(expected, category);
    }

    [Fact]
    public void GetConversions_ReturnsImageFormats_ForImage()
    {
        var conversions = CapabilityRegistry.GetConversions(["test.png"]);
        Assert.NotEmpty(conversions);
        Assert.Contains(conversions, op => op.Id == "convert:webp");
        Assert.Contains(conversions, op => op.Id == "convert:jpg");
        Assert.Contains(conversions, op => op.Id == "convert:pdf");
    }

    [Fact]
    public void GetTools_ReturnsVideoTools_ForVideo()
    {
        var tools = CapabilityRegistry.GetTools(["clip.mp4"]);
        Assert.NotEmpty(tools);
        Assert.Contains(tools, op => op.Id == "tool:trim");
        Assert.Contains(tools, op => op.Id == "tool:extract_audio");
        Assert.Contains(tools, op => op.Id == "tool:to_gif");
    }

    [Fact]
    public void FindOperation_FindsRegisteredOperation()
    {
        var op = CapabilityRegistry.FindOperation("convert:png", ["test.jpg"]);
        Assert.NotNull(op);
        Assert.Equal("PNG", op.DisplayName);
    }
}
