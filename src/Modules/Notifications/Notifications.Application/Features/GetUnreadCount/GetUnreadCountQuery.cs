using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
using Notifications.Contracts.Dtos;

namespace Notifications.Application.Features.GetUnreadCount;

public sealed record GetUnreadCountQuery(Guid CallerId, Guid? TenantId) : IQuery<Result<UnreadCountResponse>>;
