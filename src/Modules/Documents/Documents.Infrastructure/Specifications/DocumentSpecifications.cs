using BuildingBlocks.Kernel.Domain.Specifications;

using Documents.Domain.Aggregates;
using Documents.Domain.Entities;

namespace Documents.Infrastructure.Specifications;

public sealed class DocumentByTenantSpec : Specification<Document>
{
    public DocumentByTenantSpec(Guid tenantId)
    {
        Where(d => d.TenantId == tenantId);
    }
}

public sealed class DocumentByTenantAndIdSpec : Specification<Document>
{
    public DocumentByTenantAndIdSpec(Guid tenantId, Guid documentId)
    {
        Where(d => d.TenantId == tenantId && d.Id.Value == documentId);
    }
}

public sealed class AccessHistorySpec : Specification<DocumentAccessEntry>
{
    public string? ActionFilter { get; }
    public AccessHistorySpec(Guid tenantId, Guid documentId, string? action = null)
    {
        ActionFilter = action;
        Where(e => e.TenantId == tenantId && e.DocumentId.Value == documentId && (action == null || e.Action == action));
    }
}

public sealed class AuthorizedDocumentSpec : Specification<Document>
{
    public AuthorizedDocumentSpec(Guid tenantId, Guid actorId)
    {
        Where(d => d.TenantId == tenantId);
    }
}
