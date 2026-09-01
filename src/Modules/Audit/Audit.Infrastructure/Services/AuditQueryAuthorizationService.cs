using System.Linq.Expressions;
using Audit.Domain.Aggregates;
using Audit.Domain.Services;

namespace Audit.Infrastructure.Services;

public sealed class AuditQueryAuthorizationService : IAuditQueryAuthorization
{
    private readonly Func<Guid, IReadOnlySet<Guid>> _getSubtree;
    private readonly Func<Guid, IReadOnlySet<Guid>> _getProjectIds;
    public AuditQueryAuthorizationService(Func<Guid, IReadOnlySet<Guid>>? getSubtree = null, Func<Guid, IReadOnlySet<Guid>>? getProjectIds = null)
    {
        _getSubtree = getSubtree ?? (_ => new HashSet<Guid>());
        _getProjectIds = getProjectIds ?? (_ => new HashSet<Guid>());
    }

    public bool CanActorQuery(Guid actorId, Guid tenantId, object filters) => true; // Simplified: check subtree/project in BuildAuthorizedFilter

    public Expression<Func<AuditEntry, bool>> BuildAuthorizedFilter(Guid actorId, Guid tenantId)
    {
        var orgIds = _getSubtree(actorId);
        var projIds = _getProjectIds(actorId);
        return a => a.TenantId == tenantId && (a.OrganizationId == null || orgIds.Contains(a.OrganizationId.Value)) && (a.ProjectId == null || projIds.Contains(a.ProjectId.Value));
    }
}
