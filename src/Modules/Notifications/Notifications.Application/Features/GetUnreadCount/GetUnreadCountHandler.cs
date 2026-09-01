using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using Notifications.Contracts.Dtos;
using Notifications.Domain.Aggregates;
using Notifications.Domain.Ids;
using Notifications.Infrastructure.Specifications;

namespace Notifications.Application.Features.GetUnreadCount;

public sealed class GetUnreadCountHandler(IRepository<Notification, NotificationId> repo) : IQueryHandler<GetUnreadCountQuery, Result<UnreadCountResponse>>
{
    public async Task<Result<UnreadCountResponse>> HandleAsync(GetUnreadCountQuery q, CancellationToken ct)
    {
        var spec = new UnreadNotificationsSpec(q.CallerId, q.TenantId);
        var count = await repo.CountAsync(spec, ct);
        return Result.Success(new UnreadCountResponse(q.CallerId, count));
    }
}
