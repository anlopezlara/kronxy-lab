namespace Kronxy.Domain.Jobs;

public static class JobWorkflow
{
    public static JobState? DetermineNextAutomaticState(JobState currentState)
    {
        return currentState switch
        {
            JobState.Created => JobState.ContextBuilding,
            JobState.ContextBuilding => JobState.Planning,
            JobState.Planning => JobState.WorkspacePreparing,
            JobState.WorkspacePreparing => JobState.Developing,
            JobState.Developing => JobState.Building,
            JobState.Building => JobState.Testing,
            JobState.Testing => JobState.Reviewing,
            JobState.Reviewing => JobState.WaitingHuman,
            _ => null
        };
    }

    public static bool IsOperationalState(JobState state)
    {
        return state is
            JobState.ContextBuilding or
            JobState.Planning or
            JobState.WorkspacePreparing or
            JobState.Developing or
            JobState.Building or
            JobState.Testing or
            JobState.Reviewing;
    }

    public static bool IsTerminalState(JobState state)
    {
        return state is
            JobState.Completed or
            JobState.Failed or
            JobState.Rejected or
            JobState.Cancelled or
            JobState.TimedOut or
            JobState.Interrupted;
    }

    public static bool IsBaseTransitionAllowed(JobState from, JobState to)
    {
        if (to is
            JobState.Failed or
            JobState.Cancelled or
            JobState.TimedOut or
            JobState.Interrupted)
        {
            return !IsTerminalState(from);
        }

        return (from, to) switch
        {
            (JobState.Created, JobState.ContextBuilding) => true,
            (JobState.ContextBuilding, JobState.Planning) => true,
            (JobState.Planning, JobState.WorkspacePreparing) => true,
            (JobState.WorkspacePreparing, JobState.Developing) => true,
            (JobState.Developing, JobState.Building) => true,
            (JobState.Building, JobState.Testing) => true,
            (JobState.Testing, JobState.Reviewing) => true,
            (JobState.Reviewing, JobState.WaitingHuman) => true,

            (JobState.WaitingHuman, JobState.Completed) => true,
            (JobState.WaitingHuman, JobState.Rejected) => true,

            _ => false
        };
    }
}
