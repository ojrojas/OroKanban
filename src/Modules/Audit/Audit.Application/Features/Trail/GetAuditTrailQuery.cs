using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;

namespace Audit.Application.Features.Trail;

public sealed record GetAuditTrailQuery(string ResourceType, string ResourceId, string TenantId, int Page = 1, int PageSize = 50) : IQuery<Result<PagedResult<AuditTrailEntryDto>>>;
public sealed record AuditTrailEntryDto(Guid AuditId, DateTime Timestamp, string Action);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public sealed class GetAuditTrailHandler : IQueryHandler<GetAuditTrailQuery, Result<PagedResult<AuditTrailEntryDto>>>
{
    public Task<Result<PagedResult<AuditTrailEntryDto>>> HandleAsync(GetAuditTrailQuery q, CancellationToken ct)
    {
        var empty = new PagedResult<AuditTrailEntryDto>(Array.Empty<AuditTrailEntryDto>(), 0, q.Page, q.PageSize);
        return Task.FromResult(Result.Success(empty));
    }
}
