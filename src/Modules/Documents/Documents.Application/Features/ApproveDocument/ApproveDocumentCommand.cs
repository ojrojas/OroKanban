using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
namespace Documents.Application.Features.ApproveDocument;
public sealed record ApproveDocumentCommand(Guid DocumentId, Guid TenantId, Guid ActorId) : ICommand<Result<object>>;
public sealed class ApproveDocumentHandler : ICommandHandler<ApproveDocumentCommand, Result<object>>
{
    public Task<Result<object>> HandleAsync(ApproveDocumentCommand cmd, CancellationToken ct) => Task.FromResult(Result.Failure<object>(Error.Failure("NotImplemented","Not implemented")));
}
