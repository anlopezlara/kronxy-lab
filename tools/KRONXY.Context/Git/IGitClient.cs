namespace Kronxy.Context.Git;

public interface IGitClient
{
    Task<string> DiscoverRootAsync(string path, CancellationToken cancellationToken = default);
    Task<RepositoryInfo> GetRepositoryInfoAsync(string path, CancellationToken cancellationToken = default);
    Task<string> ResolveCommitAsync(string path, string reference, CancellationToken cancellationToken = default);
    Task<WorkingTreeStatus> GetWorkingTreeStatusAsync(string path, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitChange>> GetChangesAsync(
        string path,
        string fromReference,
        string toReference,
        CancellationToken cancellationToken = default);
    Task<bool> IsAncestorAsync(
        string path,
        string possibleAncestorReference,
        string descendantReference,
        CancellationToken cancellationToken = default);
}
