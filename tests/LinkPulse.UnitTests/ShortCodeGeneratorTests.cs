using LinkPulse.Api.Features.Links;

namespace LinkPulse.UnitTests;

public sealed class ShortCodeGeneratorTests
{
    [Fact]
    public void Generate_ShouldCreateDefaultLengthCode()
    {
        var generator = new ShortCodeGenerator();

        var code = generator.Generate();

        Assert.Equal(8, code.Length);
        Assert.Equal(
            code.ToLowerInvariant(),
            code);
    }

    [Fact]
    public void Generate_ShouldRespectRequestedLength()
    {
        var generator = new ShortCodeGenerator();

        var code = generator.Generate(12);

        Assert.Equal(12, code.Length);

        Assert.All(
            code,
            character =>
                Assert.True(
                    char.IsAsciiLetterOrDigit(character)));
    }
}