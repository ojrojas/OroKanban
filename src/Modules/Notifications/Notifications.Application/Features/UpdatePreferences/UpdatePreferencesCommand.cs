using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Results;
using Notifications.Domain.Enumerations;

namespace Notifications.Application.Features.UpdatePreferences;

public sealed record UpdatePreferencesCommand(Guid UserId, Guid TenantId, Dictionary<int, Dictionary<int, bool>> Preferences, byte[]? ExpectedRowVersion) : ICommand<Result<UpdatePreferencesResponse>>;

public sealed record UpdatePreferencesResponse(Guid UserId, Guid TenantId, DateTime UpdatedAt, string? RowVersion);

public sealed class UpdatePreferencesValidator : IValidator<UpdatePreferencesCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(UpdatePreferencesCommand r, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (r.UserId == Guid.Empty) failures.Add(new ValidationFailure(nameof(r.UserId), "UserId required"));
        foreach (var outer in r.Preferences)
        {
            try { NotificationType.FromId(outer.Key); } catch { failures.Add(new ValidationFailure(nameof(r.Preferences), $"Unknown NotificationType {outer.Key}")); }
            foreach (var inner in outer.Value)
            {
                try { Channel.FromId(inner.Key); } catch { failures.Add(new ValidationFailure(nameof(r.Preferences), $"Unknown Channel {inner.Key}")); }
            }
        }
        return Task.FromResult((IReadOnlyCollection<ValidationFailure>)failures);
    }
}
