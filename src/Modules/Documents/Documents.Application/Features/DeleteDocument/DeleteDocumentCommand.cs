using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
namespace Documents.Application.Features.DeleteDocument;
public sealed record DeleteDocumentCommand(Guid DocumentId, Guid TenantId, Guid ActorId) : ICommand<Result>;
public sealed class DeleteDocumentHandler : ICommandHandler<DeleteDocumentCommand, Result>
{
    public Task<Result> HandleAsync(DeleteDocumentCommand cmd, CancellationToken ct) => Task.FromResult(Result.Success());
}
