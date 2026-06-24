namespace Kronxy.Domain.Users;
public interface IUserRoleRepository
{
    Task<UserRole?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, Guid excludeId, CancellationToken cancellationToken = default);
    Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(UserRole userRole);
}
