using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Catalogs;
using Kronxy.Domain.Projects;
using Kronxy.Domain.Users;

namespace Kronxy.Application.Projects.CreateProject;

internal sealed class CreateProjectCommandHandler
    : ICommandHandler<CreateProjectCommand, Guid>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICatalogRepository _catalogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateProjectCommandHandler(
        IProjectRepository projectRepository,
        IUserRepository userRepository,
        ICatalogRepository catalogRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _catalogRepository = catalogRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Guid>> Handle(
        CreateProjectCommand request,
        CancellationToken cancellationToken)
    {
        var owner = await _userRepository.GetByIdAsync(
            request.OwnerId,
            cancellationToken);

        if (owner is null || !owner.IsActive)
        {
            return Result.Failure<Guid>(UserErrors.NotFound);
        }

        var isValidProjectType = await _catalogRepository.IsActiveItemInCatalogAsync(
            request.ProjectTypeId,
            "PROJECT_TYPE",
            cancellationToken);

        if (!isValidProjectType)
        {
            return Result.Failure<Guid>(ProjectErrors.InvalidProjectType);
        }

        var existingProject = await _projectRepository.GetByCodeAsync(
            request.Code,
            cancellationToken);

        if (existingProject is not null)
        {
            return Result.Failure<Guid>(ProjectErrors.CodeAlreadyInUse);
        }

        var project = Project.Create(
            request.Code,
            request.Name,
            request.Description,
            request.OwnerId,
            request.ProjectTypeId,
            _dateTimeProvider.UtcNow);

        _projectRepository.Add(project);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return project.Id;
    }
}