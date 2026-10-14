using System.Text.Json;
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
        Assert.Equal(750, options.MaxFilesPerPackage);
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

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    public void Api_appsettings_use_supported_inventory_limit(string fileName)
    {
        int configuredLimit = ReadApiConfiguredLimit(fileName);

        Assert.Equal(750, configuredLimit);
    }

    [Fact]
    public void Valid_environment_override_takes_precedence_over_appsettings()
    {
        const string variableName = "Context__MaxFilesPerPackage";
        int configuredLimit = ReadApiConfiguredLimit("appsettings.json");
        string? previousValue = Environment.GetEnvironmentVariable(variableName);

        try
        {
            Environment.SetEnvironmentVariable(variableName, "900");

            int effectiveLimit = ResolveLimit(
                configuredLimit,
                Environment.GetEnvironmentVariable(variableName));

            Assert.Equal(900, effectiveLimit);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, previousValue);
        }
    }

    private static int ReadApiConfiguredLimit(string fileName)
    {
        string repositoryRoot = FindRepositoryRoot();
        string path = Path.Combine(
            repositoryRoot,
            "src",
            "Kronxy.Api",
            fileName);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));

        return document.RootElement
            .GetProperty("Context")
            .GetProperty("MaxFilesPerPackage")
            .GetInt32();
    }

    private static int ResolveLimit(int configuredLimit, string? environmentOverride) =>
        int.TryParse(environmentOverride, out int value) && value > 0
            ? value
            : configuredLimit;

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Kronxy.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
