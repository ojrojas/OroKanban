using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using Notifications.Domain.Ids;
using Notifications.Domain.Aggregates;
using Notifications.Infrastructure.Specifications;

namespace Notifications.Application.Features.MarkRead;

public sealed class MarkReadHandler(IRepository<Notification, NotificationId> repo, IUnitOfWork unitOfWork) : ICommandHandler<MarkReadCommand, Result<MarkReadResponse>>
{
    public async Task<Result<MarkReadResponse>> HandleAsync(MarkReadCommand cmd, CancellationToken ct)
    {
        var spec = new NotificationByIdSpec(cmd.NotificationId);
        var notification = await repo.FirstOrDefaultAsync(spec, ct);
        if (notification is null) return Result.Failure<MarkReadResponse>(Error.NotFound("Notification.NotFound", "Notification not found"));
        if (cmd.TenantId.HasValue && notification.TenantId.HasValue && notification.TenantId != cmd.TenantId)
            return Result.Failure<MarkReadResponse>(Error.NotFound("Notification.NotFound", "Notification not found"));
        if (notification.RecipientId != cmd.CallerId)
            return Result.Failure<MarkReadResponse>(Error.Forbidden("Notification.NotOwner", "Not owner"));

        if (notification.ReadAt != null)
        {
            return Result.Success(new MarkReadResponse(notification.Id.Value, notification.RecipientId, notification.ReadAt.Value, true));
        }

        notification.MarkRead();
        repo.Update(notification);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(new MarkReadResponse(notification.Id.Value, notification.RecipientId, notification.ReadAt!.Value, false));
    }
}
