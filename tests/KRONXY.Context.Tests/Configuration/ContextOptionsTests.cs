using Kronxy.Context.Configuration;
using Xunit;

namespace Kronxy.Context.Tests.Configuration;

public sealed class ContextOptionsTests
{
    [Fact]
    public void Defaults_MatchContract()
    {
        var options = new ContextOptions();
        Assert.Equal(262_144, options.MaxTextFileBytes);
        Assert.Equal(2_000, options.MaxLinesPerFile);
        Assert.Equal(1_048_576, options.MaxPatchBytes);
        Assert.Equal(1_572_864, options.TargetHandoffPackageBytes);
        Assert.Equal(3_145_728, options.MaxHandoffPackageBytes);
        Assert.Equal(5_242_880, options.MaxBaselinePackageBytes);
        Assert.Equal(100, options.MaxFilesPerPackage);
        Assert.Equal(1, options.RelationshipDepth);
        Assert.Equal(".kronxy-context/packages", options.DefaultOutputDirectory);
        Assert.Equal("***REDACTED***", options.RedactedValue);
        Assert.Empty(options.Validate());
    }

    [Fact]
    public void Validate_CustomValidValues_ReturnsNoErrors() =>
        Assert.Empty(new ContextOptions { MaxTextFileBytes = 1, RelationshipDepth = 0 }.Validate());

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveLimit_ReturnsError(int value) =>
        Assert.NotEmpty(new ContextOptions { MaxTextFileBytes = value }.Validate());

    [Fact]
    public void Validate_TargetAboveMaximum_ReturnsError() =>
        Assert.NotEmpty(new ContextOptions { TargetHandoffPackageBytes = 11, MaxHandoffPackageBytes = 10 }.Validate());

    [Fact]
    public void Validate_NegativeRelationshipDepth_ReturnsError() =>
        Assert.NotEmpty(new ContextOptions { RelationshipDepth = -1 }.Validate());

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyOutputDirectory_ReturnsError(string value) =>
        Assert.NotEmpty(new ContextOptions { DefaultOutputDirectory = value }.Validate());

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyRedactedValue_ReturnsError(string value) =>
        Assert.NotEmpty(new ContextOptions { RedactedValue = value }.Validate());
}
