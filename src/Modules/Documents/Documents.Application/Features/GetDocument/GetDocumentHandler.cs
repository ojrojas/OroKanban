using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
namespace Documents.Application.Features.GetDocument;
public sealed class GetDocumentHandler : IQueryHandler<GetDocumentQuery, Result<DocumentResponse>>
{
    public Task<Result<DocumentResponse>> HandleAsync(GetDocumentQuery q, CancellationToken ct) => Task.FromResult(Result.Failure<DocumentResponse>(Error.Failure("NotImplemented","Not implemented")));
}
