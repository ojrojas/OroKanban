using System.Linq.Expressions;
using Audit.Domain.Aggregates;

namespace Audit.Domain.Services;

public interface IAuditMaskingPolicy
{
    ValueObjects.BeforeAfterSnapshot Mask(ValueObjects.BeforeAfterSnapshot raw);
}

public interface IAuditQueryAuthorization
{
    bool CanActorQuery(Guid actorId, Guid tenantId, object filters);
    Expression<Func<Aggregates.AuditEntry, bool>> BuildAuthorizedFilter(Guid actorId, Guid tenantId);
}
