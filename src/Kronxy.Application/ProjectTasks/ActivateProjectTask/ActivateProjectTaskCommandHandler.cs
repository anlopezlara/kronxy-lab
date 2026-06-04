using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.ProjectTasks;
namespace Kronxy.Application.ProjectTasks.ActivateProjectTask;
internal sealed class ActivateProjectTaskCommandHandler
    : ICommandHandler<ActivateProjectTaskCommand>
{
    private readonly IProjectTaskRepository _projectTaskRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    public ActivateProjectTaskCommandHandler(
        IProjectTaskRepository projectTaskRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _projectTaskRepository = projectTaskRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }
    public async Task<Result> Handle(
        ActivateProjectTaskCommand request,
        CancellationToken cancellationToken)
    {
        var projectTask = await _projectTaskRepository.GetByIdAsync(
            request.ProjectTaskId,
            cancellationToken);
        if (projectTask is null)
        {
            return Result.Failure(ProjectTaskErrors.NotFound);
        }
        projectTask.Activate(_dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
