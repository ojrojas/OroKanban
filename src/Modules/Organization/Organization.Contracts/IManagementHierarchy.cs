namespace Organization.Contracts;

public interface IManagementHierarchy
{
    Task<bool> IsInSubtreeAsync(Guid tenantId, Guid managerId, Guid userId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetSubtreeAsync(Guid tenantId, Guid managerId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetAncestorsAsync(Guid tenantId, Guid userId, CancellationToken ct);
    Task<Guid?> GetCommonAncestorAsync(Guid tenantId, Guid a, Guid b, CancellationToken ct);
}
