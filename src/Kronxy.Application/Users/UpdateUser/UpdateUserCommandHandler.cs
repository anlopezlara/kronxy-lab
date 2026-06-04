using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Catalogs;
using Kronxy.Domain.Users;

namespace Kronxy.Application.Users.UpdateUser;

internal sealed class UpdateUserCommandHandler
    : ICommandHandler<UpdateUserCommand>
{
    private const string UserRoleCatalogCode = "USER_ROLE";

    private readonly IUserRepository _userRepository;
    private readonly ICatalogRepository _catalogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateUserCommandHandler(
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

    public async Task<Result> Handle(
        UpdateUserCommand request,
        CancellationToken cancellationToken)
    {
        User? user = await _userRepository.GetByIdAsync(
            request.UserId,
            cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        bool validRole = await _catalogRepository.IsActiveItemInCatalogAsync(
            request.RoleId,
            UserRoleCatalogCode,
            cancellationToken);

        if (!validRole)
        {
            return Result.Failure(UserErrors.InvalidRole);
        }

        user.Update(
            new Username(request.Username),
            new FirstName(request.FirstName),
            new LastName(request.LastName),
            new Email(request.Email),
            string.IsNullOrWhiteSpace(request.PhoneNumber)
                ? null
                : new PhoneNumber(request.PhoneNumber),
            request.RoleId,
            _dateTimeProvider.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}