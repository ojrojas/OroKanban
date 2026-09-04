using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Projects.Contracts.Dtos;
using Projects.Domain.ValueObjects;
using Projects.Infrastructure.Persistence;

namespace ProjectsApp.Features.WorkItems.RecordTime;

public sealed record RecordTimeCommand(Guid WorkItemId, Guid TenantId, decimal ActualHours, string? Comment, Guid ActorId) : ICommand<Result<WorkItemDetailResponse>>;

public sealed class RecordTimeValidator : Validator<RecordTimeCommand>
{
    public RecordTimeValidator()
    {
        RuleFor(x => x.WorkItemId != Guid.Empty, nameof(RecordTimeCommand.WorkItemId), "WorkItemId required");
        RuleFor(x => x.ActualHours >= 0 && x.ActualHours <= 9999, nameof(RecordTimeCommand.ActualHours), "ActualHours 0..9999");
    }
}

public sealed class RecordTimeHandler(ProjectsDbContext db) : ICommandHandler<RecordTimeCommand, Result<WorkItemDetailResponse>>
{
    public async Task<Result<WorkItemDetailResponse>> HandleAsync(RecordTimeCommand cmd, CancellationToken ct)
    {
        var w = await db.WorkItems.FirstOrDefaultAsync(x => x.Id == new Projects.Domain.Ids.WorkItemId(cmd.WorkItemId) && x.TenantId == cmd.TenantId, ct);
        if (w is null) return Error.NotFound("WorkItem.NotFound", "Work item not found");
        try { _ = Effort.FromHours(cmd.ActualHours); } catch (Exception ex) { return Error.Validation("WorkItem.InvalidEffort", ex.Message); }
        var before = w.ActualHours;
        w.RecordTime(cmd.ActualHours, cmd.ActorId);
        db.WorkItemHistories.Add(new Projects.Domain.Aggregates.WorkItemHistory(w.Id.Value, w.TenantId, cmd.ActorId, "ActualHours", System.Text.Json.JsonSerializer.Serialize(before), System.Text.Json.JsonSerializer.Serialize(cmd.ActualHours), cmd.Comment));
        await db.SaveChangesAsync(ct);
        var dto = new WorkItemDetailResponse(w.Id.Value, w.ProjectId, w.ParentId, w.Title, w.Description,
            Projects.Domain.Enumerations.WorkItemType.FromId(w.TypeId).Name,
            Projects.Domain.Enumerations.WorkItemStatus.FromId(w.StatusId).Name,
            Projects.Domain.Enumerations.WorkItemPriority.FromId(w.PriorityId).Name,
            Projects.Domain.Enumerations.Criticality.FromId(w.CriticalityId).Name,
            w.OwnerId, w.ResponsibleId, w.ReviewerId, w.DueDate, w.ProgressPercent, w.Tags, w.Deliverables, w.Observations, w.Version, w.UpdatedAt, w.TenantId, w.IsOverdue(DateTime.UtcNow), [], w.EstimatedHours, w.ActualHours, w.StartedAt, w.ReopenedCount, false);
        return Result.Success(dto);
    }
}
