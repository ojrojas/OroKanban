using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;

namespace Audit.Application.Features.Timeline;

public sealed record GetOperationTimelineQuery(Guid CorrelationId, string TenantId) : IQuery<Result<IReadOnlyList<TimelineEntryDto>>>;
public sealed record TimelineEntryDto(Guid AuditId, DateTime Timestamp, string Action, string ResourceType, string ResourceId);

public sealed class GetOperationTimelineHandler : IQueryHandler<GetOperationTimelineQuery, Result<IReadOnlyList<TimelineEntryDto>>>
{
    public Task<Result<IReadOnlyList<TimelineEntryDto>>> HandleAsync(GetOperationTimelineQuery q, CancellationToken ct)
    {
        IReadOnlyList<TimelineEntryDto> empty = Array.Empty<TimelineEntryDto>();
        return Task.FromResult(Result.Success(empty));
    }
}
