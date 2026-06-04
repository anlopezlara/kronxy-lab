using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Catalogs;
using Kronxy.Domain.Projects;
using Kronxy.Domain.Users;

namespace Kronxy.Application.Projects.UpdateProject;

internal sealed class UpdateProjectCommandHandler
    : ICommandHandler<UpdateProjectCommand>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICatalogRepository _catalogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateProjectCommandHandler(
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

    public async Task<Result> Handle(
        UpdateProjectCommand request,
        CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(
            request.ProjectId,
            cancellationToken);

        if (project is null)
        {
            return Result.Failure(ProjectErrors.NotFound);
        }

        var owner = await _userRepository.GetByIdAsync(
            request.OwnerId,
            cancellationToken);

        if (owner is null || !owner.IsActive)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        var isValidProjectType = await _catalogRepository.IsActiveItemInCatalogAsync(
            request.ProjectTypeId,
            "PROJECT_TYPE",
            cancellationToken);

        if (!isValidProjectType)
        {
            return Result.Failure(ProjectErrors.InvalidProjectType);
        }

        var isValidProjectStatus = await _catalogRepository.IsActiveItemInCatalogAsync(
            request.ProjectStatusId,
            "PROJECT_STATUS",
            cancellationToken);

        if (!isValidProjectStatus)
        {
            return Result.Failure(ProjectErrors.InvalidProjectStatus);
        }

        var existingProject = await _projectRepository.GetByCodeAsync(
            request.Code,
            cancellationToken);

        if (existingProject is not null &&
            existingProject.Id != project.Id)
        {
            return Result.Failure(ProjectErrors.CodeAlreadyInUse);
        }

        project.Update(
            request.Code,
            request.Name,
            request.Description,
            request.OwnerId,
            request.ProjectTypeId,
            request.ProjectStatusId,
            _dateTimeProvider.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}