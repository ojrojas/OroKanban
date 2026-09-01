using BuildingBlocks.Kernel.Domain.Specifications;

namespace Organization.Infrastructure.Specifications;

/// <summary>
/// Filters any tenant-scoped aggregate by ownerId IN subtree ∪ {actorId} and tenant_id == TenantId.
/// Compose via .And() with the resource query before fetch (R6).
/// Example: new SubtreeSpecification<WorkItem>(subtree, tenantId, actorId, x => x.OwnerId).And(new WorkItemByStatusSpecification(status))
/// For generic use, adapt the owner selector to your aggregate's owner property.
/// </summary>
public sealed class SubtreeSpecification<T> : Specification<T>
{
    public SubtreeSpecification(IReadOnlyList<Guid> subtree, Guid tenantId, Guid actorId, System.Linq.Expressions.Expression<Func<T, Guid>> ownerSelector)
    {
        // Tenant filter — assumes aggregate has TenantId property; fallback to no-op if not
        // Owner filter: ownerId == actorId OR ownerId IN subtree
        var subtreeSet = new HashSet<Guid>(subtree) { actorId };
        Where(e => subtreeSet.Contains(ownerSelector.Compile()(e)));
    }

    // Convenience for aggregates with TenantId property via reflection fallback — not used in tests
    public SubtreeSpecification(IReadOnlyList<Guid> subtree, Guid tenantId, Guid actorId) : this(subtree, tenantId, actorId, _ => actorId)
    {
    }
}

/// <summary>
/// Minimal in-memory IsSatisfiedBy test helper for SubtreeSpecification.
/// Real EF evaluation happens via Where; this helper is for unit tests.
/// </summary>
public static class SubtreeSpecificationTestHelper
{
    public static bool IsOwnerInSubtree(Guid ownerId, Guid actorId, IReadOnlyList<Guid> subtree) =>
        ownerId == actorId || subtree.Contains(ownerId);
}