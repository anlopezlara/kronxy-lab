using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Catalogs;
using Kronxy.Domain.Users;

namespace Kronxy.Application.Users.CreateUser;

internal sealed class CreateUserCommandHandler
    : ICommandHandler<CreateUserCommand, Guid>
{
    private const string UserRoleCatalogCode = "USER_ROLE";

    private readonly IUserRepository _userRepository;
    private readonly ICatalogRepository _catalogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateUserCommandHandler(
        IUserRepository userRepository,
        ICatalogRepository catalogRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _userRepository = userRepository;
        _catalogRepository = catalogRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Guid>> Handle(
        CreateUserCommand request,
        CancellationToken cancellationToken)
    {
        bool validRole = await _catalogRepository.IsActiveItemInCatalogAsync(
            request.RoleId,
            UserRoleCatalogCode,
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