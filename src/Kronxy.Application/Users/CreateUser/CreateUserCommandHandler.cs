using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Users;
namespace Kronxy.Application.Users.CreateUser;
internal sealed class CreateUserCommandHandler
    : ICommandHandler<CreateUserCommand, Guid>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    public CreateUserCommandHandler(
        IUserRepository userRepository,
        IUserRoleRepository userRoleRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }
    public async Task<Result<Guid>> Handle(
        CreateUserCommand request,
        CancellationToken cancellationToken)
    {
        bool validRole = await _userRoleRepository.IsActiveAsync(
            request.RoleId,
            cancellationToken);
        if (!validRole)
        {
            return Result.Failure<Guid>(UserErrors.InvalidRole);
        }
        var user = User.Create(
            new Username(request.Username),
            new FirstName(request.FirstName),
            new LastName(request.LastName),
            new Email(request.Email),
            string.IsNullOrWhiteSpace(request.PhoneNumber)
                ? null
                : new PhoneNumber(request.PhoneNumber),
            request.RoleId,
            _dateTimeProvider.UtcNow);
        _userRepository.Add(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return user.Id;
    }
}
