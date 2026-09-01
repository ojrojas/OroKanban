using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
namespace Documents.Application.Features.AccessHistory;
public sealed record GetAccessHistoryQuery(Guid DocumentId, Guid TenantId, Guid ActorId, int Page, int PageSize, string? Action) : IQuery<Result<PagedHistoryResponse>>;
public sealed record HistoryEntry(Guid Id, string Action, bool Granted, string Classification);
public sealed record PagedHistoryResponse(IReadOnlyList<HistoryEntry> Items, int TotalCount, int Page, int PageSize);
public sealed class GetAccessHistoryHandler : IQueryHandler<GetAccessHistoryQuery, Result<PagedHistoryResponse>>
{
    public Task<Result<PagedHistoryResponse>> HandleAsync(GetAccessHistoryQuery q, CancellationToken ct) => Task.FromResult(Result.Failure<PagedHistoryResponse>(Error.Failure("NotImplemented","Not implemented")));
}
