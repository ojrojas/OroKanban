using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Results;

namespace Audit.Application.Features.Correction;

public sealed record CreateAuditCorrectionCommand(Guid CorrectedAuditId, string CorrectedResult, string Rationale, Guid TenantId, Guid ActorId) : ICommand<Result<Guid>>;

public sealed class CreateAuditCorrectionValidator : IValidator<CreateAuditCorrectionCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(CreateAuditCorrectionCommand r, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (string.IsNullOrWhiteSpace(r.Rationale) || r.Rationale.Length > 2000) failures.Add(new ValidationFailure(nameof(r.Rationale), "Rationale 1..2000"));
        return Task.FromResult((IReadOnlyCollection<ValidationFailure>)failures);
    }
}

public sealed class CreateAuditCorrectionHandler : ICommandHandler<CreateAuditCorrectionCommand, Result<Guid>>
{
    public Task<Result<Guid>> HandleAsync(CreateAuditCorrectionCommand cmd, CancellationToken ct) => Task.FromResult(Result.Success(Guid.NewGuid()));
}
