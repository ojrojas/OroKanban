using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Results;

namespace AiProcessing.Application.Features.QueueOperation;

public sealed record QueueLlmOperationCommand(Guid DocumentId, Guid DocumentVersionId, string OperationType, Guid TenantId, Guid ActorId) : ICommand<Result<QueueLlmOperationResponse>>;
public sealed record QueueLlmOperationResponse(Guid OperationId, Guid CorrelationId, string OperationStatus);

public sealed class QueueLlmOperationValidator : IValidator<QueueLlmOperationCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(QueueLlmOperationCommand request, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (request.DocumentId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.DocumentId), "DocumentId required"));
        if (string.IsNullOrWhiteSpace(request.OperationType)) failures.Add(new ValidationFailure(nameof(request.OperationType), "OperationType required"));
        return Task.FromResult((IReadOnlyCollection<ValidationFailure>)failures);
    }
}

public sealed class QueueLlmOperationHandler : ICommandHandler<QueueLlmOperationCommand, Result<QueueLlmOperationResponse>>
{
    public Task<Result<QueueLlmOperationResponse>> HandleAsync(QueueLlmOperationCommand cmd, CancellationToken ct)
    {
        var opId = Guid.NewGuid();
        return Task.FromResult(Result.Success(new QueueLlmOperationResponse(opId, opId, "Queued")));
    }
}
