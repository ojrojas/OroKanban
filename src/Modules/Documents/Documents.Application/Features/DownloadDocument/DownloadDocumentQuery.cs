using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
namespace Documents.Application.Features.DownloadDocument;
public sealed record DownloadDocumentQuery(Guid DocumentId, int? VersionNumber, Guid TenantId, Guid ActorId) : IQuery<Result<Stream>>;
public sealed class DownloadDocumentHandler : IQueryHandler<DownloadDocumentQuery, Result<Stream>>
{
    public Task<Result<Stream>> HandleAsync(DownloadDocumentQuery q, CancellationToken ct) => Task.FromResult(Result.Failure<Stream>(Error.Failure("NotImplemented","Not implemented")));
}
