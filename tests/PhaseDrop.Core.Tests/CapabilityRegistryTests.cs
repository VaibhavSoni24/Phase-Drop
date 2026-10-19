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

    [Fact]
    public void GetConversions_Pdf_DoesNotOfferUnsupportedRasterization_AndOffersArchives()
    {
        var conversions = CapabilityRegistry.GetConversions(["sample.pdf"]);
        Assert.NotEmpty(conversions);
        // Ensure no fake rasterization claims
        Assert.DoesNotContain(conversions, op => op.Id == "convert:png");
        Assert.DoesNotContain(conversions, op => op.Id == "convert:jpg");
        // Ensure genuine archive options
        Assert.Contains(conversions, op => op.Id == "convert:zip");
        Assert.Contains(conversions, op => op.Id == "convert:7z");

        // Verify PDF tools are genuine
        var tools = CapabilityRegistry.GetTools(["sample.pdf"]);
        Assert.Contains(tools, op => op.Id == "tool:merge_pdf");
        Assert.Contains(tools, op => op.Id == "tool:split_pdf");
        Assert.Contains(tools, op => op.Id == "tool:extract_pages");
        Assert.Contains(tools, op => op.Id == "tool:rotate_pages");
        Assert.Contains(tools, op => op.Id == "tool:strip_metadata");
    }
}
