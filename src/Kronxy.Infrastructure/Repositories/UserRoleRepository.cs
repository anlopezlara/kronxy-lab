using Kronxy.Domain.Users;
using Microsoft.EntityFrameworkCore;
namespace Kronxy.Infrastructure.Repositories;
internal sealed class UserRoleRepository : Repository<UserRole>, IUserRoleRepository
{
    public UserRoleRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }
    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        string normalizedCode = code.Trim().ToUpperInvariant();
        return await DbContext.Set<UserRole>()
            .AnyAsync(userRole => userRole.Code == normalizedCode, cancellationToken);
    }
    public async Task<bool> ExistsByCodeAsync(string code, Guid excludeId, CancellationToken cancellationToken = default)
    {
        string normalizedCode = code.Trim().ToUpperInvariant();
        return await DbContext.Set<UserRole>()
            .AnyAsync(userRole =>
                userRole.Code == normalizedCode &&
                userRole.Id != excludeId,
                cancellationToken);
    }
    public async Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<UserRole>()
            .AnyAsync(userRole =>
                userRole.Id == id &&
                userRole.IsActive,
                cancellationToken);
    }
}
