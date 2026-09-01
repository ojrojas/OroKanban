namespace Organization.Domain.Services;

public interface IProjectMembership
{
    Task<bool> IsMemberAsync(Guid tenantId, Guid userId, Guid resourceId, CancellationToken ct);
}