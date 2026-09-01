using Projects.Domain.Enumerations;
using Projects.Domain.Services;

namespace Projects.Infrastructure.Services;

public sealed class WorkItemTransitionPolicy : IWorkItemTransitionPolicy
{
    private readonly Dictionary<int, HashSet<int>> _map = new()
    {
        [WorkItemStatus.Backlog.Id] = [WorkItemStatus.Planned.Id],
        [WorkItemStatus.Planned.Id] = [WorkItemStatus.InProgress.Id],
        [WorkItemStatus.InProgress.Id] = [WorkItemStatus.Blocked.Id, WorkItemStatus.InReview.Id],
        [WorkItemStatus.Blocked.Id] = [WorkItemStatus.InReview.Id, WorkItemStatus.InProgress.Id],
        [WorkItemStatus.InReview.Id] = [WorkItemStatus.Blocked.Id, WorkItemStatus.Completed.Id],
        [WorkItemStatus.Completed.Id] = [WorkItemStatus.InProgress.Id], // reopen
    };

    public bool IsAllowed(int fromId, int toId) =>
        _map.TryGetValue(fromId, out var set) && set.Contains(toId);

    public IReadOnlySet<int> AllowedFrom(int fromId) =>
        _map.TryGetValue(fromId, out var set) ? set : new HashSet<int>();
}