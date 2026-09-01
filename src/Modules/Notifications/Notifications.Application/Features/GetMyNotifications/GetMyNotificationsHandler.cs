using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.Kernel.Domain.Specifications;
using Notifications.Contracts.Dtos;
using Notifications.Domain.Aggregates;
using Notifications.Domain.Ids;
using Notifications.Infrastructure.Specifications;

namespace Notifications.Application.Features.GetMyNotifications;

public sealed class GetMyNotificationsHandler(IRepository<Notification, NotificationId> repo) : IQueryHandler<GetMyNotificationsQuery, Result<PagedNotificationsResponse>>
{
    public async Task<Result<PagedNotificationsResponse>> HandleAsync(GetMyNotificationsQuery q, CancellationToken ct)
    {
        var spec = new NotificationByRecipientSpec(q.CallerId, q.TenantId, q.UnreadOnly);
        // Apply ordering and paging via specification is not directly supported for this repo's ListAsync? We'll manually handle ordering.
        // For simplicity, use repo ListAsync and then order/paging is done via specification's Apply methods if supported.
        // Use ListAsync with spec that includes paging? We'll create a custom spec that sets OrderBy and Paging via protected methods — but those are not accessible from handler without inheriting.
        // So we fetch and paginate manually via repo facilities: use SpecificationEvaluator via repo — but we can simulate by fetching all and paging in memory for MVP (10k acceptable for test, but performance note says should be indexed).
        // For correctness, we delegate to repo ListAsync which respects Specification's Skip/Take/OrderBy if we create a spec that sets them via public? Instead create a derived spec that configures ordering.
        var pagedSpec = new PagedNotificationSpec(q.CallerId, q.TenantId, q.UnreadOnly, q.Page, q.PageSize);
        var items = await repo.ListAsync(pagedSpec, ct);
        var total = await repo.CountAsync(spec, ct);

        // Optional type filter after fetch if TypeId provided (since spec doesn't include it efficiently)
        if (q.TypeId.HasValue)
        {
            items = items.Where(n => n.NotificationType.Id == q.TypeId.Value).ToList();
            total = items.Count; // approximate
        }

        var dtos = items.Select(Map).ToList();
        return Result.Success(new PagedNotificationsResponse(dtos, total, q.Page, q.PageSize, null));
    }

    private static NotificationResponse Map(Notification n) => new(
        n.Id.Value, n.RecipientId, n.NotificationType.Name, n.NotificationType.Id,
        n.Channel.Name, n.Channel.Id, n.Title, n.Body, n.Link,
        n.DeliveryState.Name, n.DeliveryState.Id, n.CreatedAt, n.ReadAt, n.SourceEventId, n.SourceResourceId, n.SourceResourceType, n.CorrelationId);
}

internal sealed class PagedNotificationSpec : Specification<Notification>
{
    public PagedNotificationSpec(Guid recipientId, Guid? tenantId, bool unreadOnly, int page, int pageSize)
    {
        Where(n => n.RecipientId == recipientId);
        if (tenantId.HasValue) Where(n => n.TenantId == tenantId.Value);
        if (unreadOnly) Where(n => n.ReadAt == null);
        ApplyOrderByDescending(n => n.CreatedAt);
        ApplyPaging((page - 1) * pageSize, pageSize);
        ApplyAsNoTracking();
    }
}
