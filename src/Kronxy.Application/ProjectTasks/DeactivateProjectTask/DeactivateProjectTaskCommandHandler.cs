using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.ProjectTasks;
namespace Kronxy.Application.ProjectTasks.DeactivateProjectTask;
internal sealed class DeactivateProjectTaskCommandHandler
    : ICommandHandler<DeactivateProjectTaskCommand>
{
    private readonly IProjectTaskRepository _projectTaskRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    public DeactivateProjectTaskCommandHandler(
        IProjectTaskRepository projectTaskRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _projectTaskRepository = projectTaskRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }
    public async Task<Result> Handle(
        DeactivateProjectTaskCommand request,
        CancellationToken cancellationToken)
    {
        var projectTask = await _projectTaskRepository.GetByIdAsync(
            request.ProjectTaskId,
            cancellationToken);
        if (projectTask is null)
        {
            return Result.Failure(ProjectTaskErrors.NotFound);
        }
        projectTask.Deactivate(_dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
