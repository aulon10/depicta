using Depicta.Api.Vision;
using Xunit;

namespace Depicta.Tests.Unit;

/// <summary>
/// The sanitizer is the guarantee behind the prompt: whatever the model returns, downstream
/// always gets contract-compliant alt text (≤125 chars, no "image of …" filler).
/// </summary>
public class AltTextSanitizerTests
{
    [Theory]
    [InlineData("Image of a red bicycle by a wall", "A red bicycle by a wall")]
    [InlineData("a photo of two dogs.", "Two dogs")]
    [InlineData("Picture showing a sunset over the sea", "A sunset over the sea")]
    [InlineData("  spaced   out   text ", "Spaced out text")]
    public void Clean_strips_filler_and_normalizes(string input, string expected)
        => Assert.Equal(expected, AltTextSanitizer.Clean(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Clean_returns_empty_for_blank(string? input)
        => Assert.Equal(string.Empty, AltTextSanitizer.Clean(input));

    [Fact]
    public void Clean_caps_at_125_characters()
    {
        var result = AltTextSanitizer.Clean(new string('a', 300));
        Assert.True(result.Length <= AltTextSanitizer.MaxLength);
    }

    [Fact]
    public void Clean_drops_a_single_trailing_period()
        => Assert.Equal("A cat on a mat", AltTextSanitizer.Clean("A cat on a mat."));

    [Fact]
    public void IsCompliant_flags_filler_and_overlength()
    {
        Assert.True(AltTextSanitizer.IsCompliant("A tidy phrase"));
        Assert.False(AltTextSanitizer.IsCompliant("image of something"));
        Assert.False(AltTextSanitizer.IsCompliant(new string('a', 200)));
    }
}
