using Kronxy.Context.Commands;
using Kronxy.Context.Models;
using Xunit;

namespace Kronxy.Context.Tests.Commands;

public sealed class CommandLineParserTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Parse_HelpFlags_RequestHelp(string flag)
    {
        var result = CommandLineParser.Parse([flag]);
        Assert.True(result.Success);
        Assert.True(result.HelpRequested);
    }

    [Fact]
    public void Parse_Version_RequestsVersion()
    {
        var result = CommandLineParser.Parse(["--version"]);
        Assert.True(result.Success);
        Assert.True(result.VersionRequested);
    }

    [Fact]
    public void Parse_NoArguments_ReturnsError() =>
        Assert.False(CommandLineParser.Parse([]).Success);

    [Fact]
    public void Parse_UnknownCommand_ReturnsError() =>
        Assert.False(CommandLineParser.Parse(["unknown"]).Success);

    [Theory]
    [MemberData(nameof(ValidCommands))]
    public void Parse_EachCommand_ReturnsTypedRequest(string[] args, ContextCommand expected)
    {
        var result = CommandLineParser.Parse(args);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(expected, result.Request!.Command);
    }

    public static TheoryData<string[], ContextCommand> ValidCommands => new()
    {
        { ["init", "--repository", "repo"], ContextCommand.Init },
        { ["baseline", "--repository", "repo"], ContextCommand.Baseline },
        { ["handoff", "--repository", "repo", "--work-item", "1"], ContextCommand.Handoff },
        { ["issue", "--repository", "repo", "--work-item", "1", "--error-text", "failed"], ContextCommand.Issue },
        { ["validate", "--package", "package.zip"], ContextCommand.Validate },
        { ["accept", "--package", "package.zip"], ContextCommand.Accept }
    };

    [Fact]
    public void Parse_IsCaseInsensitive()
    {
        var result = CommandLineParser.Parse(["HaNdOfF", "--REPOSITORY", "repo", "--WORK-ITEM", "1"]);
        Assert.True(result.Success, result.ErrorMessage);
    }

    [Fact]
    public void Parse_OptionWithSeparateValue_PreservesSpaces()
    {
        var result = CommandLineParser.Parse(["init", "--repository", @"E:\a path\repo"]);
        Assert.Equal(@"E:\a path\repo", result.Request!.Repository);
    }

    [Fact]
    public void Parse_OptionWithEquals_PreservesSpaces()
    {
        var result = CommandLineParser.Parse(["init", @"--repository=E:\a path\repo"]);
        Assert.Equal(@"E:\a path\repo", result.Request!.Repository);
    }

    [Fact]
    public void Parse_UnknownOption_ReturnsError() =>
        Assert.False(CommandLineParser.Parse(["init", "--repository", "repo", "--unknown"]).Success);

    [Fact]
    public void Parse_RepeatedOption_ReturnsError() =>
        Assert.False(CommandLineParser.Parse(["init", "--repository", "one", "--repository", "two"]).Success);

    [Fact]
    public void Parse_MissingValue_ReturnsError() =>
        Assert.False(CommandLineParser.Parse(["init", "--repository"]).Success);

    [Fact]
    public void Parse_EmptyValue_ReturnsError() =>
        Assert.False(CommandLineParser.Parse(["init", "--repository="]).Success);

    [Fact]
    public void Parse_DraftAndFinal_ReturnsError() =>
        Assert.False(CommandLineParser.Parse(["baseline", "--repository", "repo", "--draft", "--final"]).Success);

    [Theory]
    [InlineData("init", "--package")]
    [InlineData("baseline", "--from")]
    [InlineData("handoff", "--ref")]
    [InlineData("issue", "--package")]
    [InlineData("validate", "--repository")]
    [InlineData("accept", "--final")]
    public void Parse_IncompatibleOption_ReturnsError(string command, string option)
    {
        var args = option is "--final" ? new[] { command, "--package", "p", option } : new[] { command, option, "value" };
        Assert.False(CommandLineParser.Parse(args).Success);
    }

    [Theory]
    [InlineData("init")]
    [InlineData("baseline")]
    [InlineData("handoff")]
    [InlineData("issue")]
    public void Parse_MissingRepository_ReturnsError(string command) =>
        Assert.False(CommandLineParser.Parse([command]).Success);

    [Fact]
    public void Parse_HandoffMissingWorkItem_ReturnsError() =>
        Assert.False(CommandLineParser.Parse(["handoff", "--repository", "repo"]).Success);

    [Fact]
    public void Parse_IssueMissingWorkItem_ReturnsError() =>
        Assert.False(CommandLineParser.Parse(["issue", "--repository", "repo", "--error-text", "failed"]).Success);

    [Fact]
    public void Parse_IssueMissingErrorSource_ReturnsError() =>
        Assert.False(CommandLineParser.Parse(["issue", "--repository", "repo", "--work-item", "1"]).Success);

    [Theory]
    [InlineData("validate")]
    [InlineData("accept")]
    public void Parse_PackageCommandMissingPackage_ReturnsError(string command) =>
        Assert.False(CommandLineParser.Parse([command]).Success);

    [Fact]
    public void Parse_Baseline_AppliesDefaults()
    {
        var request = CommandLineParser.Parse(["baseline", "--repository", "repo"]).Request!;
        Assert.Equal("HEAD", request.Ref);
        Assert.Equal(PackageStability.Draft, request.Stability);
    }

    [Theory]
    [InlineData("handoff")]
    [InlineData("issue")]
    public void Parse_IncrementalCommand_AppliesDefaults(string command)
    {
        var args = command == "issue"
            ? new[] { command, "--repository", "repo", "--work-item", "1", "--error-text", "failed" }
            : new[] { command, "--repository", "repo", "--work-item", "1" };
        var request = CommandLineParser.Parse(args).Request!;
        Assert.Equal("last", request.From);
        Assert.Equal("HEAD", request.To);
        Assert.Equal(PackageStability.Draft, request.Stability);
    }

    [Fact]
    public void Parse_Final_OverridesDefaultStability()
    {
        var request = CommandLineParser.Parse(["baseline", "--repository", "repo", "--final"]).Request!;
        Assert.Equal(PackageStability.Final, request.Stability);
    }
}
