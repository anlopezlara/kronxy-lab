using Kronxy.Infrastructure.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class DeveloperChangePolicyOptionsTests
{
    [Fact]
    public void Safe_defaults_are_valid()
    {
        var options =
            new DeveloperChangePolicyOptions();

        options.Validate();

        Assert.Equal(4, options.MaxOperations);
        Assert.Equal(3, options.MaxCreatedFiles);
        Assert.Equal(262_144, options.MaxFileBytes);
        Assert.Equal(
            1_048_576,
            options.MaxTotalChangeBytes);
        Assert.Contains(
            "AGENTS.md",
            options.ProtectedFileNames);
        Assert.Contains(
            ".git",
            options.ProtectedPathPrefixes);
    }

    [Fact]
    public void Invalid_limits_fail_closed()
    {
        Assert.Throws<InvalidOperationException>(
            () =>
                new DeveloperChangePolicyOptions
                {
                    MaxOperations = 0
                }.Validate());

        Assert.Throws<InvalidOperationException>(
            () =>
                new DeveloperChangePolicyOptions
                {
                    MaxOperations = 2,
                    MaxCreatedFiles = 3
                }.Validate());

        Assert.Throws<InvalidOperationException>(
            () =>
                new DeveloperChangePolicyOptions
                {
                    MaxFileBytes = 1024,
                    MaxTotalChangeBytes = 512
                }.Validate());

        Assert.Throws<InvalidOperationException>(
            () =>
                new DeveloperChangePolicyOptions
                {
                    MaxTotalChangeBytes = 4096,
                    MaxProposalBytes = 2048
                }.Validate());
    }

    [Fact]
    public void Unsafe_protected_configuration_fails_closed()
    {
        Assert.Throws<InvalidOperationException>(
            () =>
                new DeveloperChangePolicyOptions
                {
                    ProtectedPathPrefixes =
                    [
                        "../outside"
                    ]
                }.Validate());

        Assert.Throws<InvalidOperationException>(
            () =>
                new DeveloperChangePolicyOptions
                {
                    ProtectedFileNames =
                    [
                        "safe.txt",
                        "folder/unsafe.txt"
                    ]
                }.Validate());

        Assert.Throws<InvalidOperationException>(
            () =>
                new DeveloperChangePolicyOptions
                {
                    ProtectedPathPrefixes =
                    [
                        ".git",
                        ".GIT"
                    ]
                }.Validate());
    }
}
