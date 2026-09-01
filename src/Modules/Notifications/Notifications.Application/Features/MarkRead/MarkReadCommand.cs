using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Results;

namespace Notifications.Application.Features.MarkRead;

public sealed record MarkReadCommand(Guid NotificationId, Guid CallerId, Guid? TenantId) : ICommand<Result<MarkReadResponse>>;

public sealed record MarkReadResponse(Guid NotificationId, Guid RecipientId, DateTime ReadAt, bool WasAlreadyRead);

public sealed class MarkReadValidator : IValidator<MarkReadCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(MarkReadCommand r, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (r.NotificationId == Guid.Empty) failures.Add(new ValidationFailure(nameof(r.NotificationId), "Invalid Guid"));
        if (r.CallerId == Guid.Empty) failures.Add(new ValidationFailure(nameof(r.CallerId), "CallerId required"));
        return Task.FromResult((IReadOnlyCollection<ValidationFailure>)failures);
    }
}
