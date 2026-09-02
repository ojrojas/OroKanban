using Documents.Domain.Enumerations;

namespace Documents.Domain.Services;

public sealed class DocumentAccessPolicy : IDocumentAccessPolicy
{
    private readonly Func<Guid, Guid, Task<bool>> _isInSubtree;
    private readonly Func<Guid, Guid, Task<bool>> _isMember;
    private readonly Func<Guid, Guid, Task<bool>> _hasExplicitGrant;

    public DocumentAccessPolicy(
        Func<Guid, Guid, Task<bool>>? isInSubtree = null,
        Func<Guid, Guid, Task<bool>>? isMember = null,
        Func<Guid, Guid, Task<bool>>? hasExplicitGrant = null)
    {
        _isInSubtree = isInSubtree ?? ((_, _) => Task.FromResult(false));
        _isMember = isMember ?? ((_, _) => Task.FromResult(false));
        _hasExplicitGrant = hasExplicitGrant ?? ((_, _) => Task.FromResult(false));
    }

    public async Task<AccessDecision> EvaluateAsync(AccessContext ctx, CancellationToken ct)
    {
        // 0 IsSafe gate — deny NotSafe even if all other checks pass
        if (!ctx.IsSafe || ctx.ScanStatus != ScanStatus.Safe.Name)
            return new AccessDecision(false, "NotSafe");

        // 1 tenant mismatch → 404 shadow
        if (ctx.TenantId != ctx.DocumentTenantId)
            return new AccessDecision(false, "TenantMismatch");

        // 2 owner → grant
        if (ctx.ActorId == ctx.OwnerId)
            return new AccessDecision(true, "Owner");

        // 3 explicit grant → grant
        if (await _hasExplicitGrant(ctx.DocumentId, ctx.ActorId))
            return new AccessDecision(true, "ExplicitGrant");

        // 4 classification clearance
        var maxLevel = GetMaxLevel(ctx.ActorRoles);
        if (ctx.ClassificationLevelId > maxLevel)
            return new AccessDecision(false, "InsufficientClassification");

        // 5 subtree OR 6 membership
        if (ctx.ProjectId is not null && await _isMember(ctx.ProjectId.Value, ctx.ActorId))
            return new AccessDecision(true, "ProjectMembership");
        if (await _isInSubtree(ctx.OwnerId, ctx.ActorId))
            return new AccessDecision(true, "Subtree");

        return new AccessDecision(false, "NotInSubtreeOrMembership");
    }

    private static int GetMaxLevel(IReadOnlySet<string> roles)
    {
        // Simplified mapping: viewer=2, manager=3, restrictedReader=4, auditor=5 etc.
        if (roles.Contains("admin") || roles.Contains("auditor")) return 100;
        if (roles.Contains("restrictedReader") || roles.Contains("approver")) return 4;
        if (roles.Contains("manager")) return 3;
        if (roles.Contains("viewer")) return 2;
        return 1;
    }
}
