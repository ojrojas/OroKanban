using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
namespace Documents.Application.Features.ListDocumentVersions;
public sealed class ListDocumentVersionsHandler : IQueryHandler<ListDocumentVersionsQuery, Result<PagedResult<DocumentVersionResponse>>>
{
    public Task<Result<PagedResult<DocumentVersionResponse>>> HandleAsync(ListDocumentVersionsQuery q, CancellationToken ct) => Task.FromResult(Result.Failure<PagedResult<DocumentVersionResponse>>(Error.Failure("NotImplemented","Not implemented")));
}
