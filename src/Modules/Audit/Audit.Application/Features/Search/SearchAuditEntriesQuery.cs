using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Results;

namespace Audit.Application.Features.Search;

public sealed record SearchAuditEntriesQuery(Guid? ActorId, string? Action, string? ResourceType, string? ResourceId, Guid? ProjectId, Guid? OrganizationId, DateTime? From, DateTime? To, string? Result, Guid? CorrelationId, int Page = 1, int PageSize = 50, string TenantId = "") : IQuery<Result<PagedResult<AuditEntryDto>>>;
public sealed record AuditEntryDto(Guid AuditId, DateTime Timestamp, string Action, string ResourceType, string ResourceId, string Result, Guid CorrelationId);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public sealed class SearchAuditEntriesValidator : IValidator<SearchAuditEntriesQuery>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(SearchAuditEntriesQuery r, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (r.From.HasValue && r.To.HasValue && r.From > r.To) failures.Add(new ValidationFailure(nameof(r.From), "From > To"));
        if (r.Page < 1 || r.Page > 100) failures.Add(new ValidationFailure(nameof(r.Page), "Page 1..100"));
        if (r.PageSize < 1 || r.PageSize > 100) failures.Add(new ValidationFailure(nameof(r.PageSize), "PageSize 1..100"));
        return Task.FromResult((IReadOnlyCollection<ValidationFailure>)failures);
    }
}

public sealed class SearchAuditEntriesHandler : IQueryHandler<SearchAuditEntriesQuery, Result<PagedResult<AuditEntryDto>>>
{
    public Task<Result<PagedResult<AuditEntryDto>>> HandleAsync(SearchAuditEntriesQuery q, CancellationToken ct)
    {
        var empty = new PagedResult<AuditEntryDto>(Array.Empty<AuditEntryDto>(), 0, q.Page, q.PageSize);
        return Task.FromResult(Result.Success(empty));
    }
}
