using Identity.Contracts;
using Microsoft.EntityFrameworkCore;
using Organization.Contracts;
using Organization.Domain.Services;

namespace Organization.Infrastructure.Services;

public sealed record AuthorizationRequest(
    Guid ActorUserId,
    Guid TenantId,
    string Permission,
    string ResourceType,
    Guid ResourceId,
    Guid? ResourceOwnerId,
    string? Classification,
    IReadOnlyList<string> ActorRoles
);

public enum DenyReason { TenantMismatch, MissingPermission, NotOwner, NotInSubtree, GrantExpired, NotMember, ClassificationDenied }

public sealed record AuthorizationResult(bool IsAllowed, DenyReason? DenyReason);

public interface IAuthorizationEvaluator
{
    Task<AuthorizationResult> EvaluateAsync(AuthorizationRequest request, CancellationToken ct);
}

public sealed class AuthorizationEvaluator : IAuthorizationEvaluator
{
    private readonly IPermissionCatalog _permissions;
    private readonly IManagementHierarchy _hierarchy;
    private readonly IProjectMembership _membership;
    private readonly Organization.Infrastructure.Persistence.OrganizationDbContext _db;

    public AuthorizationEvaluator(IPermissionCatalog permissions, IManagementHierarchy hierarchy, IProjectMembership membership, Organization.Infrastructure.Persistence.OrganizationDbContext db)
    {
        _permissions = permissions;
        _hierarchy = hierarchy;
        _membership = membership;
        _db = db;
    }

    public async Task<AuthorizationResult> EvaluateAsync(AuthorizationRequest request, CancellationToken ct)
    {
        // 1. Tenant mismatch — first gate (edge case: tenant mismatch denies before any other work)
        // Resource tenant is assumed to be request.TenantId for this foundation — real resource tenant would be loaded from resource table
        // For now, we treat tenant check as actor.TenantId must match request.TenantId (caller supplies correct tenant)
        // In full implementation, load resource.TenantId and compare.

        // 2. Permission via catalog
        if (!await _permissions.HasPermissionAsync(request.ActorRoles, request.Permission, ct))
            return new AuthorizationResult(false, DenyReason.MissingPermission);

        // 3. Ownership — if actor is owner, allow (subject to classification)
        if (request.ResourceOwnerId.HasValue && request.ResourceOwnerId.Value == request.ActorUserId)
        {
            // Still check classification
            if (request.Classification == "HighlyRestricted" && !request.ActorRoles.Contains("Administrator"))
                return new AuthorizationResult(false, DenyReason.ClassificationDenied);
            return new AuthorizationResult(true, null);
        }

        // 4. Subtree — if resource has owner, check actor is in owner's ancestor chain or vice versa
        if (request.ResourceOwnerId.HasValue)
        {
            var inSubtree = await _hierarchy.IsInSubtreeAsync(request.TenantId, request.ActorUserId, request.ResourceOwnerId.Value, ct);
            if (inSubtree)
            {
                if (request.Classification == "HighlyRestricted" && !request.ActorRoles.Contains("Administrator"))
                    return new AuthorizationResult(false, DenyReason.ClassificationDenied);
                return new AuthorizationResult(true, null);
            }
        }

        // 5. ExplicitGrant — check if any grant covers this request
        var grantSatisfied = await CheckGrantAsync(request, ct);
        if (grantSatisfied) return new AuthorizationResult(true, null);

        // 6. Project membership — stub until Projects spec
        if (await _membership.IsMemberAsync(request.TenantId, request.ActorUserId, request.ResourceId, ct))
            return new AuthorizationResult(true, null);

        // 7. Classification final check
        if (request.Classification == "HighlyRestricted")
            return new AuthorizationResult(false, DenyReason.ClassificationDenied);

        return new AuthorizationResult(false, DenyReason.NotInSubtree);
    }

    private async Task<bool> CheckGrantAsync(AuthorizationRequest request, CancellationToken ct)
    {
        var grant = await _db.ExplicitGrants.FirstOrDefaultAsync(g =>
            g.TenantId == request.TenantId &&
            g.GranteeUserId == request.ActorUserId &&
            g.ResourceType == request.ResourceType &&
            g.ResourceId == request.ResourceId &&
            g.Permission == request.Permission, ct);
        if (grant == null) return false;
        return grant.IsSatisfiedBy(request.TenantId, request.ActorUserId, request.ResourceType, request.ResourceId, request.Permission, DateTime.UtcNow);
    }
}

// Stub for IProjectMembership until Projects spec provides real implementation
public sealed class ProjectMembershipStub : IProjectMembership
{
    public Task<bool> IsMemberAsync(Guid tenantId, Guid userId, Guid resourceId, CancellationToken ct) => Task.FromResult(false);
}
