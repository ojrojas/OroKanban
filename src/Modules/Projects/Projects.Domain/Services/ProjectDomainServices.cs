using BuildingBlocks.Kernel.Domain.Results;

namespace Projects.Domain.Services;

public interface IWorkItemTransitionPolicy
{
    bool IsAllowed(int fromId, int toId);
    IReadOnlySet<int> AllowedFrom(int fromId);
}

public interface IDependencyCycleDetector
{
    bool HasCycle(IReadOnlyList<(Guid DependentId, Guid PrincipalId, int TypeId)> existingEdges, (Guid DependentId, Guid PrincipalId, int TypeId) candidate);
}

public interface IHierarchyInspector
{
    Task<IReadOnlySet<Guid>> GetAncestorIdsAsync(Guid workItemId, CancellationToken ct);
    Task<IReadOnlySet<Guid>> GetDescendantIdsAsync(Guid workItemId, CancellationToken ct);
    Task<Guid?> GetRootEpicIdAsync(Guid workItemId, CancellationToken ct);
}

public interface IAssignmentPolicy
{
    Task<Result> CanAssignAsync(Guid assignerId, Guid assigneeId, Guid projectId, Guid tenantId, int statusId, CancellationToken ct);
}

public interface IProjectMembership
{
    Task<bool> IsMemberAsync(Guid userId, Guid projectId, CancellationToken ct);
    Task<IReadOnlySet<Guid>> GetProjectIdsForUserAsync(Guid userId, CancellationToken ct);
}

public interface IUserStateChecker
{
    Task<bool> IsActiveAsync(Guid userId, CancellationToken ct);
}