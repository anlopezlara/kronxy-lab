using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.ProjectTasks;
using Kronxy.Domain.Users;
namespace Kronxy.Application.ProjectTasks.UpdateProjectTask;
internal sealed class UpdateProjectTaskCommandHandler
    : ICommandHandler<UpdateProjectTaskCommand>
{
    private readonly IProjectTaskRepository _projectTaskRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    public UpdateProjectTaskCommandHandler(
        IProjectTaskRepository projectTaskRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _projectTaskRepository = projectTaskRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }
    public async Task<Result> Handle(
        UpdateProjectTaskCommand request,
        CancellationToken cancellationToken)
    {
        var projectTask = await _projectTaskRepository.GetByIdAsync(
            request.ProjectTaskId,
            cancellationToken);
        if (projectTask is null)
        {
            return Result.Failure(ProjectTaskErrors.NotFound);
        }
        var user = await _userRepository.GetByIdAsync(
            request.AssignedUserId,
            cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result.Failure(UserErrors.NotFound);
        }
        projectTask.Update(
            request.AssignedUserId,
            request.Title,
            request.Description,
            _dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
