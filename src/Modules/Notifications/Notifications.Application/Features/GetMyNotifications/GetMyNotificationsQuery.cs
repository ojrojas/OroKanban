using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Results;
using Notifications.Contracts.Dtos;

namespace Notifications.Application.Features.GetMyNotifications;

public sealed record GetMyNotificationsQuery(Guid CallerId, Guid? TenantId, int Page = 1, int PageSize = 20, bool UnreadOnly = false, int? TypeId = null) : IQuery<Result<PagedNotificationsResponse>>;

public sealed class GetMyNotificationsValidator : IValidator<GetMyNotificationsQuery>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(GetMyNotificationsQuery r, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (r.CallerId == Guid.Empty) failures.Add(new ValidationFailure(nameof(r.CallerId), "CallerId required"));
        if (r.Page < 1 || r.Page > 100) failures.Add(new ValidationFailure(nameof(r.Page), "Page 1..100"));
        if (r.PageSize < 1 || r.PageSize > 100) failures.Add(new ValidationFailure(nameof(r.PageSize), "PageSize 1..100"));
        return Task.FromResult((IReadOnlyCollection<ValidationFailure>)failures);
    }
}
