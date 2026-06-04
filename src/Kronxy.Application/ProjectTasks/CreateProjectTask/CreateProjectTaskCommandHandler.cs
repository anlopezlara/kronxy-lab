using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Projects;
using Kronxy.Domain.ProjectTasks;
using Kronxy.Domain.Users;
namespace Kronxy.Application.ProjectTasks.CreateProjectTask;
internal sealed class CreateProjectTaskCommandHandler
    : ICommandHandler<CreateProjectTaskCommand, Guid>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectTaskRepository _projectTaskRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    public CreateProjectTaskCommandHandler(
        IProjectRepository projectRepository,
        IProjectTaskRepository projectTaskRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _projectRepository = projectRepository;
        _projectTaskRepository = projectTaskRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }
    public async Task<Result<Guid>> Handle(
        CreateProjectTaskCommand request,
        CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);
        if (project is null || !project.IsActive)
        {
            return Result.Failure<Guid>(ProjectErrors.NotFound);
        }
        var user = await _userRepository.GetByIdAsync(request.AssignedUserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result.Failure<Guid>(UserErrors.NotFound);
        }
        var projectTask = ProjectTask.Create(
            request.ProjectId,
            request.AssignedUserId,
            request.Title,
            request.Description,
            _dateTimeProvider.UtcNow);
        _projectTaskRepository.Add(projectTask);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return projectTask.Id;
    }
}
