using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Projects;
using Kronxy.Domain.Users;
namespace Kronxy.Application.Projects.UpdateProject;
internal sealed class UpdateProjectCommandHandler
    : ICommandHandler<UpdateProjectCommand>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly IProjectTypeRepository _projectTypeRepository;
    private readonly IProjectStatusRepository _projectStatusRepository;
    private readonly IProjectPriorityRepository _projectPriorityRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    public UpdateProjectCommandHandler(
        IProjectRepository projectRepository,
        IUserRepository userRepository,
        IProjectTypeRepository projectTypeRepository,
        IProjectStatusRepository projectStatusRepository,
        IProjectPriorityRepository projectPriorityRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _projectTypeRepository = projectTypeRepository;
        _projectStatusRepository = projectStatusRepository;
        _projectPriorityRepository = projectPriorityRepository;
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
        if (!await _projectTypeRepository.IsActiveAsync(request.ProjectTypeId, cancellationToken))
        {
            return Result.Failure(ProjectErrors.InvalidProjectType);
        }
        if (!await _projectStatusRepository.IsActiveAsync(request.ProjectStatusId, cancellationToken))
        {
            return Result.Failure(ProjectErrors.InvalidProjectStatus);
        }
        if (!await _projectPriorityRepository.IsActiveAsync(request.ProjectPriorityId, cancellationToken))
        {
            return Result.Failure(ProjectErrors.InvalidProjectPriority);
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
            request.ProjectPriorityId,
            _dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
