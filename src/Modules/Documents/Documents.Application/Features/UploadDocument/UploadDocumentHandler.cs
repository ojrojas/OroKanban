using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
namespace Documents.Application.Features.UploadDocument;
public sealed class UploadDocumentHandler : ICommandHandler<UploadDocumentCommand, Result<UploadDocumentResponse>>
{
    public Task<Result<UploadDocumentResponse>> HandleAsync(UploadDocumentCommand request, CancellationToken ct) => Task.FromResult(Result.Failure<UploadDocumentResponse>(Error.Failure("NotImplemented","Upload not yet fully implemented")));
}
