namespace LinkPulse.UnitTests;

public sealed class ProgramAssemblyTests
{
    [Fact]
    public void Program_ShouldBeLocatedInApiAssembly()
    {
        var assemblyName = typeof(Program).Assembly.GetName().Name;

        Assert.Equal("LinkPulse.Api", assemblyName);
    }
}