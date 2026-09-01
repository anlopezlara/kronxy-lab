namespace Kronxy.ControlPlane.Tests;

internal static class TestToolResolver
{
    public static string Dotnet()
    {
        string? hostPath =
            Environment.GetEnvironmentVariable(
                "DOTNET_HOST_PATH");

        if (IsUsableAbsoluteFile(hostPath))
        {
            return Path.GetFullPath(hostPath!);
        }

        string? dotnetRoot =
            Environment.GetEnvironmentVariable(
                "DOTNET_ROOT");

        if (!string.IsNullOrWhiteSpace(dotnetRoot))
        {
            string candidate =
                Path.Combine(
                    dotnetRoot,
                    OperatingSystem.IsWindows()
                        ? "dotnet.exe"
                        : "dotnet");

            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return ResolveFromPath(
            OperatingSystem.IsWindows()
                ? "dotnet.exe"
                : "dotnet");
    }

    public static string Git()
    {
        return ResolveFromPath(
            OperatingSystem.IsWindows()
                ? "git.exe"
                : "git");
    }

    private static bool IsUsableAbsoluteFile(
        string? value)
    {
        return
            !string.IsNullOrWhiteSpace(value) &&
            Path.IsPathFullyQualified(value) &&
            File.Exists(value);
    }

    private static string ResolveFromPath(
        string executable)
    {
        string? pathValue =
            Environment.GetEnvironmentVariable(
                "PATH");

        if (string.IsNullOrWhiteSpace(pathValue))
        {
            throw new InvalidOperationException(
                $"PATH is unavailable while resolving {executable}.");
        }

        foreach (string directory in
                 pathValue.Split(
                     Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            string candidate =
                Path.Combine(
                    directory,
                    executable);

            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        throw new InvalidOperationException(
            $"{executable} executable not found.");
    }
}
