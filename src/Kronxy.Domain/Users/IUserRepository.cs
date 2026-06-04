using UserEntity = Kronxy.Domain.Users.User;
using UserEmail = Kronxy.Domain.Users.Email;
using UserUsername = Kronxy.Domain.Users.Username;

namespace Kronxy.Domain.Users;

public interface IUserRepository
{
    Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UserEntity?> GetByEmailAsync(UserEmail email, CancellationToken cancellationToken = default);

    Task<UserEntity?> GetByUsernameAsync(UserUsername username, CancellationToken cancellationToken = default);

    void Add(UserEntity user);
}