using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;

namespace AiProcessing.Application.Features.GetResultHistory;

public sealed record GetResultHistoryQuery(Guid DocumentVersionId, Guid TenantId, int Page = 1, int PageSize = 20) : IQuery<Result<PagedResult<LlmResultHistoryDto>>>;
public sealed record LlmResultHistoryDto(Guid ResultId, Guid OperationId, string OperationType, string ReviewStatus, object Provenance);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public sealed class GetResultHistoryHandler : IQueryHandler<GetResultHistoryQuery, Result<PagedResult<LlmResultHistoryDto>>>
{
    public Task<Result<PagedResult<LlmResultHistoryDto>>> HandleAsync(GetResultHistoryQuery q, CancellationToken ct)
    {
        var empty = new PagedResult<LlmResultHistoryDto>(Array.Empty<LlmResultHistoryDto>(), 0, q.Page, q.PageSize);
        return Task.FromResult(Result.Success(empty));
    }
}
