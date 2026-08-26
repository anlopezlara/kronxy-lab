using Kronxy.Context.Files;
using Xunit;

namespace Kronxy.Context.Tests.Files;

public sealed class RepositoryPathResolverTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "kronxy-path-tests", Guid.NewGuid().ToString("N"));
    private readonly RepositoryPathResolver resolver = new();

    public RepositoryPathResolverTests() => Directory.CreateDirectory(root);

    [Theory]
    [InlineData("file.txt", "file.txt")]
    [InlineData("sub/folder/file.txt", "sub/folder/file.txt")]
    [InlineData("space name.txt", "space name.txt")]
    [InlineData("Unicode-ñ.txt", "Unicode-ñ.txt")]
    [InlineData("-option.txt", "-option.txt")]
    [InlineData("sub\\mixed/file.txt", "sub/mixed/file.txt")]
    public void Resolve_AcceptsSafeRelativePaths(string input, string normalized)
    {
        var result = resolver.Resolve(root, input);
        Assert.True(result.Success);
        Assert.Equal(normalized, result.LogicalPath);
        Assert.StartsWith(Path.GetFullPath(root), result.PhysicalPath!, OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../secret.txt")]
    [InlineData("sub/../secret.txt")]
    [InlineData("./file.txt")]
    [InlineData("sub/./file.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("C:\\absolute.txt")]
    [InlineData("\\\\server\\share\\file.txt")]
    [InlineData("\\\\?\\C:\\device.txt")]
    [InlineData("bad\0name.txt")]
    [InlineData("bad\nname.txt")]
    public void Resolve_RejectsUnsafePaths(string path) => Assert.False(resolver.Resolve(root, path).Success);

    [Fact]
    public void Resolve_DoesNotAcceptCommonPrefixOutsideRoot()
    {
        var result = resolver.Resolve(root, "../" + Path.GetFileName(root) + "-other/file.txt");
        Assert.False(result.Success);
    }

    [Fact]
    public void Resolve_RejectsAlternateDataStreamOnWindows()
    {
        if (OperatingSystem.IsWindows()) Assert.False(resolver.Resolve(root, "file.txt:stream").Success);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con.txt")]
    [InlineData("CON .txt")]
    [InlineData("PRN.log")]
    [InlineData("AUX")]
    [InlineData("NUL.json")]
    [InlineData("COM1.txt")]
    [InlineData("LPT9")]
    [InlineData("trailing-space ")]
    [InlineData("trailing-dot.")]
    public void Resolve_AppliesWindowsReservedNameRulesOnlyOnWindows(string path)
    {
        Assert.Equal(!OperatingSystem.IsWindows(), resolver.Resolve(root, path).Success);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
        DeleteEmptyParent(root);
    }

    private static void DeleteEmptyParent(string path)
    {
        var parent = Path.GetDirectoryName(path)!;
        if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
    }
}
