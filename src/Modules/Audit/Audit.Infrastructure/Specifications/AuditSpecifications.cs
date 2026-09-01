using BuildingBlocks.Kernel.Domain.Specifications;
using Audit.Domain.Aggregates;

namespace Audit.Infrastructure.Specifications;

public sealed class AuditByTenantSpec : Specification<AuditEntry>
{
    public AuditByTenantSpec(Guid tenantId) { Where(a => a.TenantId == tenantId); }
}

public sealed class AuditByTenantAndResourceSpec : Specification<AuditEntry>
{
    public AuditByTenantAndResourceSpec(Guid tenantId, string resourceType, string resourceId)
    {
        Where(a => a.TenantId == tenantId && a.ResourceType == resourceType && a.ResourceId == resourceId);
    }
}

public sealed class AuditByCorrelationIdSpec : Specification<AuditEntry>
{
    public AuditByCorrelationIdSpec(Guid tenantId, Guid correlationId)
    {
        Where(a => a.TenantId == tenantId && a.CorrelationId == correlationId);
    }
}

public sealed class AuditByTenantAndTimestampRangeSpec : Specification<AuditEntry>
{
    public AuditByTenantAndTimestampRangeSpec(Guid tenantId, DateTime? from, DateTime? to)
    {
        Where(a => a.TenantId == tenantId && (!from.HasValue || a.Timestamp >= from) && (!to.HasValue || a.Timestamp <= to));
    }
}
