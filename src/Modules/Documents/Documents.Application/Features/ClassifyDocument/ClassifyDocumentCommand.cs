using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
namespace Documents.Application.Features.ClassifyDocument;
public sealed record ClassifyDocumentCommand(Guid DocumentId, string Classification, Guid TenantId, Guid ActorId) : ICommand<Result<object>>;
public sealed class ClassifyDocumentHandler : ICommandHandler<ClassifyDocumentCommand, Result<object>>
{
    public Task<Result<object>> HandleAsync(ClassifyDocumentCommand cmd, CancellationToken ct) => Task.FromResult(Result.Failure<object>(Error.Failure("NotImplemented","Not implemented")));
}
