using Microsoft.EntityFrameworkCore;
using UserEntity = Kronxy.Domain.Users.User;
using UserEmail = Kronxy.Domain.Users.Email;
using UserUsername = Kronxy.Domain.Users.Username;
using Kronxy.Domain.Users;

namespace Kronxy.Infrastructure.Repositories;

internal sealed class UserRepository : Repository<UserEntity>, IUserRepository
{
    public UserRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<UserEntity?> GetByEmailAsync(
        UserEmail email,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<UserEntity>()
            .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
    }

    public async Task<UserEntity?> GetByUsernameAsync(
        UserUsername username,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<UserEntity>()
            .FirstOrDefaultAsync(user => user.Username == username, cancellationToken);
    }
}