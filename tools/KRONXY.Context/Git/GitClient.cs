using System.Globalization;
using System.Text.RegularExpressions;
using Kronxy.Context.Processes;

namespace Kronxy.Context.Git;

public sealed partial class GitClient : IGitClient
{
    private const int MaximumReferenceLength = 1_024;
    private const int GitOutputLimitBytes = 8_388_608;
    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(30);
    private readonly IProcessRunner processRunner;

    public GitClient(IProcessRunner processRunner)
    {
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task<string> DiscoverRootAsync(string path, CancellationToken cancellationToken = default)
    {
        var workingDirectory = ValidatePath(path);
        var result = await RunGitAsync(workingDirectory, ["rev-parse", "--show-toplevel"], cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(result, GitErrorKind.NotARepository, "La ruta no pertenece a un repositorio Git.");

        var root = ParseSingleLine(result.StandardOutput);
        if (!Path.IsPathFullyQualified(root))
        {
            throw new GitClientException(GitErrorKind.MalformedOutput, "Git devolvió una raíz no válida.");
        }

        return Path.GetFullPath(root);
    }

    public async Task<RepositoryInfo> GetRepositoryInfoAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var root = await DiscoverRootAsync(path, cancellationToken).ConfigureAwait(false);
        string head;
        try
        {
            head = await ResolveCommitAsync(root, "HEAD", cancellationToken).ConfigureAwait(false);
        }
        catch (GitClientException exception) when (exception.Kind == GitErrorKind.ReferenceNotFound)
        {
            throw new GitClientException(
                GitErrorKind.NoInitialCommit,
                "El repositorio todavía no contiene un commit inicial.");
        }

        var branchResult = await RunGitAsync(
            root,
            ["symbolic-ref", "--quiet", "--short", "HEAD"],
            cancellationToken).ConfigureAwait(false);

        if (branchResult.ExitCode is not 0 and not 1)
        {
            EnsureSuccess(branchResult, GitErrorKind.CommandFailed, "Git no pudo consultar la rama actual.");
        }

        var detached = branchResult.ExitCode == 1;
        return new RepositoryInfo
        {
            RootPath = root,
            HeadCommit = head,
            Branch = detached ? null : ParseSingleLine(branchResult.StandardOutput),
            IsHeadDetached = detached
        };
    }

    public async Task<string> ResolveCommitAsync(
        string path,
        string reference,
        CancellationToken cancellationToken = default)
    {
        ValidateReference(reference);
        var root = await DiscoverRootAsync(path, cancellationToken).ConfigureAwait(false);
        var objectResult = await RunGitAsync(
            root,
            ["rev-parse", "--verify", "--end-of-options", reference],
            cancellationToken).ConfigureAwait(false);
        EnsureSuccess(objectResult, GitErrorKind.ReferenceNotFound, "La referencia Git no existe.");
        var objectId = ParseObjectId(objectResult.StandardOutput);

        var commitResult = await RunGitAsync(
            root,
            ["rev-parse", "--verify", "--end-of-options", string.Concat(objectId, "^{commit}")],
            cancellationToken).ConfigureAwait(false);
        EnsureSuccess(commitResult, GitErrorKind.ReferenceNotFound, "La referencia no identifica un commit.");
        return ParseObjectId(commitResult.StandardOutput);
    }

    public async Task<WorkingTreeStatus> GetWorkingTreeStatusAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var root = await DiscoverRootAsync(path, cancellationToken).ConfigureAwait(false);
        var result = await RunGitAsync(
            root,
            ["status", "--porcelain=v2", "-z", "--untracked-files=all"],
            cancellationToken).ConfigureAwait(false);
        EnsureSuccess(result, GitErrorKind.CommandFailed, "Git no pudo consultar el working tree.");
        return GitStatusParser.Parse(result.StandardOutput);
    }

    public async Task<IReadOnlyList<GitIndexEntry>> GetIndexEntriesAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var root = await DiscoverRootAsync(path, cancellationToken).ConfigureAwait(false);
        var result = await RunGitAsync(root, ["ls-files", "--stage", "-z"], cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(result, GitErrorKind.CommandFailed, "Git no pudo consultar el índice.");
        return GitIndexParser.Parse(result.StandardOutput);
    }

    public async Task<IReadOnlyList<string>> GetUntrackedFilesAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var root = await DiscoverRootAsync(path, cancellationToken).ConfigureAwait(false);
        var result = await RunGitAsync(
            root,
            ["ls-files", "--others", "--exclude-standard", "-z"],
            cancellationToken).ConfigureAwait(false);
        EnsureSuccess(result, GitErrorKind.CommandFailed, "Git no pudo consultar archivos no rastreados.");
        return GitPathListParser.Parse(result.StandardOutput);
    }

    public async Task<IReadOnlyList<GitChange>> GetChangesAsync(
        string path,
        string fromReference,
        string toReference,
        CancellationToken cancellationToken = default)
    {
        var root = await DiscoverRootAsync(path, cancellationToken).ConfigureAwait(false);
        var fromCommit = await ResolveCommitAsync(root, fromReference, cancellationToken).ConfigureAwait(false);
        var toCommit = await ResolveCommitAsync(root, toReference, cancellationToken).ConfigureAwait(false);
        var result = await RunGitAsync(
            root,
            ["diff", "--name-status", "-z", "--find-renames", "--no-ext-diff", "--no-textconv", fromCommit, toCommit, "--"],
            cancellationToken).ConfigureAwait(false);
        EnsureSuccess(result, GitErrorKind.CommandFailed, "Git no pudo obtener las diferencias solicitadas.");
        return GitDiffParser.Parse(result.StandardOutput);
    }

    public async Task<bool> IsAncestorAsync(
        string path,
        string possibleAncestorReference,
        string descendantReference,
        CancellationToken cancellationToken = default)
    {
        var root = await DiscoverRootAsync(path, cancellationToken).ConfigureAwait(false);
        var ancestor = await ResolveCommitAsync(root, possibleAncestorReference, cancellationToken).ConfigureAwait(false);
        var descendant = await ResolveCommitAsync(root, descendantReference, cancellationToken).ConfigureAwait(false);
        var result = await RunGitAsync(
            root,
            ["merge-base", "--is-ancestor", ancestor, descendant],
            cancellationToken).ConfigureAwait(false);

        return result.ExitCode switch
        {
            0 => true,
            1 => false,
            _ => throw CreateCommandException(result, GitErrorKind.CommandFailed, "Git no pudo comprobar la relación de ancestro.")
        };
    }

    private async Task<ProcessResult> RunGitAsync(
        string workingDirectory,
        IReadOnlyList<string> operationArguments,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "--no-pager",
            "--no-optional-locks",
            "-c", "color.ui=false",
            "-c", "core.pager=cat",
            "-c", "core.fsmonitor=false",
            "-c", "credential.interactive=never"
        };
        arguments.AddRange(operationArguments);

        try
        {
            return await processRunner.RunAsync(new ProcessRequest
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                Arguments = arguments,
                Timeout = GitTimeout,
                StandardOutputLimitBytes = GitOutputLimitBytes,
                StandardErrorLimitBytes = ProcessRequest.DefaultOutputLimitBytes,
                EnvironmentVariables = new Dictionary<string, string?>
                {
                    ["GIT_OPTIONAL_LOCKS"] = "0",
                    ["GIT_TERMINAL_PROMPT"] = "0",
                    ["GCM_INTERACTIVE"] = "Never",
                    ["GIT_PAGER"] = "cat"
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (ProcessRunnerException)
        {
            throw new GitClientException(
                GitErrorKind.GitUnavailable,
                "Git no está instalado o no pudo iniciarse.");
        }
    }

    private static string ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\0'))
        {
            throw new GitClientException(GitErrorKind.PathNotFound, "La ruta indicada no es válida.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new GitClientException(GitErrorKind.PathNotFound, "La ruta indicada no es válida.");
        }

        if (!Directory.Exists(fullPath))
        {
            throw new GitClientException(GitErrorKind.PathNotFound, "La ruta indicada no existe.");
        }

        return fullPath;
    }

    private static void ValidateReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference) ||
            reference.Length > MaximumReferenceLength ||
            reference[0] == '-' ||
            reference.IndexOfAny(['\0', '\r', '\n']) >= 0)
        {
            throw new GitClientException(GitErrorKind.InvalidReference, "La referencia Git no es válida.");
        }
    }

    private static string ParseObjectId(string output)
    {
        var value = ParseSingleLine(output);
        if ((value.Length is not 40 and not 64) || !ObjectIdRegex().IsMatch(value))
        {
            throw new GitClientException(GitErrorKind.MalformedOutput, "Git devolvió un identificador de objeto no válido.");
        }

        return value.ToLower(CultureInfo.InvariantCulture);
    }

    private static string ParseSingleLine(string output)
    {
        var value = output.TrimEnd('\r', '\n');
        if (string.IsNullOrEmpty(value) || value.Contains('\0') || value.Contains('\r') || value.Contains('\n'))
        {
            throw new GitClientException(GitErrorKind.MalformedOutput, "Git devolvió una salida no válida.");
        }

        return value;
    }

    private static void EnsureSuccess(
        ProcessResult result,
        GitErrorKind nonZeroKind,
        string nonZeroMessage)
    {
        if (result.ExitCode == 0 && result.TerminationCause == ProcessTerminationCause.Completed)
        {
            return;
        }

        throw CreateCommandException(result, nonZeroKind, nonZeroMessage);
    }

    private static GitClientException CreateCommandException(
        ProcessResult result,
        GitErrorKind nonZeroKind,
        string nonZeroMessage) => result.TerminationCause switch
        {
            ProcessTerminationCause.TimedOut => new GitClientException(
                GitErrorKind.TimedOut,
                "La operación Git superó el tiempo permitido."),
            ProcessTerminationCause.StandardOutputLimitExceeded or
                ProcessTerminationCause.StandardErrorLimitExceeded => new GitClientException(
                    GitErrorKind.OutputLimitExceeded,
                    "La salida de Git superó el límite permitido."),
            _ => new GitClientException(nonZeroKind, nonZeroMessage)
        };

    [GeneratedRegex("\\A[0-9a-fA-F]+\\z", RegexOptions.CultureInvariant)]
    private static partial Regex ObjectIdRegex();
}
