using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Projects;
using Kronxy.Domain.Users;
namespace Kronxy.Application.Projects.CreateProject;
internal sealed class CreateProjectCommandHandler
    : ICommandHandler<CreateProjectCommand, Guid>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly IProjectTypeRepository _projectTypeRepository;
    private readonly IProjectStatusRepository _projectStatusRepository;
    private readonly IProjectPriorityRepository _projectPriorityRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    public CreateProjectCommandHandler(
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
        if (!await _projectTypeRepository.IsActiveAsync(request.ProjectTypeId, cancellationToken))
        {
            return Result.Failure<Guid>(ProjectErrors.InvalidProjectType);
        }
        if (!await _projectStatusRepository.IsActiveAsync(request.ProjectStatusId, cancellationToken))
        {
            return Result.Failure<Guid>(ProjectErrors.InvalidProjectStatus);
        }
        if (!await _projectPriorityRepository.IsActiveAsync(request.ProjectPriorityId, cancellationToken))
        {
            return Result.Failure<Guid>(ProjectErrors.InvalidProjectPriority);
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
            request.ProjectStatusId,
            request.ProjectPriorityId,
            _dateTimeProvider.UtcNow);
        _projectRepository.Add(project);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return project.Id;
    }
}
