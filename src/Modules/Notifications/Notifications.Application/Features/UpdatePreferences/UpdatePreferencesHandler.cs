using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using Notifications.Domain.Aggregates;
using Notifications.Infrastructure.Specifications;

namespace Notifications.Application.Features.UpdatePreferences;

public sealed class UpdatePreferencesHandler(IRepository<NotificationPreference, Guid> repo, IUnitOfWork uow) : ICommandHandler<UpdatePreferencesCommand, Result<UpdatePreferencesResponse>>
{
    public async Task<Result<UpdatePreferencesResponse>> HandleAsync(UpdatePreferencesCommand cmd, CancellationToken ct)
    {
        var spec = new PreferenceByUserSpec(cmd.UserId);
        var existing = await repo.FirstOrDefaultAsync(spec, ct);
        if (existing is null)
        {
            var created = NotificationPreference.Create(cmd.UserId, cmd.TenantId, cmd.Preferences);
            await repo.AddAsync(created, ct);
            await uow.SaveChangesAsync(ct);
            return Result.Success(new UpdatePreferencesResponse(created.Id, created.TenantId, created.UpdatedAt, created.RowVersion != null ? Convert.ToBase64String(created.RowVersion) : null));
        }

        if (cmd.ExpectedRowVersion != null && existing.RowVersion != null && !cmd.ExpectedRowVersion.SequenceEqual(existing.RowVersion))
        {
            return Result.Failure<UpdatePreferencesResponse>(Error.Conflict("Preferences.Concurrency", "Preferences were modified concurrently"));
        }

        existing.Update(cmd.Preferences);
        repo.Update(existing);
        await uow.SaveChangesAsync(ct);
        return Result.Success(new UpdatePreferencesResponse(existing.Id, existing.TenantId, existing.UpdatedAt, existing.RowVersion != null ? Convert.ToBase64String(existing.RowVersion) : null));
    }
}
