using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
namespace Documents.Application.Features.PublishVersion;
public sealed record PublishDocumentVersionCommand(Guid DocumentId, byte[]? Content, Guid TenantId, Guid ActorId) : ICommand<Result<PublishVersionResponse>>;
public sealed record PublishVersionResponse(Guid DocumentId, Guid VersionId, int VersionNumber, string ContentHash);
public sealed class PublishDocumentVersionHandler : ICommandHandler<PublishDocumentVersionCommand, Result<PublishVersionResponse>>
{
    public Task<Result<PublishVersionResponse>> HandleAsync(PublishDocumentVersionCommand cmd, CancellationToken ct) => Task.FromResult(Result.Failure<PublishVersionResponse>(Error.Failure("NotImplemented","Not implemented")));
}
