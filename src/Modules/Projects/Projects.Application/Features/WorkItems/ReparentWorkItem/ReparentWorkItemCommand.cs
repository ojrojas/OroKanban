using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Results;

using Microsoft.EntityFrameworkCore;

using Projects.Contracts.Dtos;
using Projects.Domain.Services;
using Projects.Infrastructure.Persistence;

namespace ProjectsApp.Features.WorkItems.ReparentWorkItem;

public sealed record ReparentWorkItemCommand(Guid WorkItemId, Guid TenantId, Guid? NewParentId, int ExpectedVersion) : ICommand<Result<WorkItemDetailResponse>>;

public sealed class ReparentWorkItemValidator : Validator<ReparentWorkItemCommand>
{
    public ReparentWorkItemValidator()
    {
        RuleFor(x => x.WorkItemId != Guid.Empty, nameof(ReparentWorkItemCommand.WorkItemId), "WorkItemId required");
        RuleFor(x => x.ExpectedVersion > 0, nameof(ReparentWorkItemCommand.ExpectedVersion), "ExpectedVersion required");
    }
}

public sealed class ReparentWorkItemHandler(ProjectsDbContext db, IHierarchyInspector inspector) : ICommandHandler<ReparentWorkItemCommand, Result<WorkItemDetailResponse>>
{
    public async Task<Result<WorkItemDetailResponse>> HandleAsync(ReparentWorkItemCommand cmd, CancellationToken ct)
    {
        var w = await db.WorkItems.FirstOrDefaultAsync(x => x.Id == new Projects.Domain.Ids.WorkItemId(cmd.WorkItemId) && x.TenantId == cmd.TenantId, ct);
        if (w is null) return Error.NotFound("WorkItem.NotFound", "Work item not found");
        if (w.Version != cmd.ExpectedVersion) return Error.Conflict("WorkItem.Concurrency", "Concurrency conflict");

        if (cmd.NewParentId.HasValue)
        {
            var parent = await db.WorkItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == new Projects.Domain.Ids.WorkItemId(cmd.NewParentId.Value), ct);
            if (parent is null) return Error.NotFound("WorkItem.ParentNotFound", "Parent not found");
            if (parent.ProjectId != w.ProjectId) return Error.Validation("WorkItem.CrossProjectParent", "Parent and child must be in same project");
            var descendants = await inspector.GetDescendantIdsAsync(cmd.WorkItemId, ct);
            if (descendants.Contains(cmd.NewParentId.Value))
                return Error.Validation("WorkItem.ReparentCycle", "Cannot reparent to descendant");
        }

        w.Reparent(cmd.NewParentId);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Error.Conflict("WorkItem.Concurrency", "Concurrency conflict"); }

        var dto = new WorkItemDetailResponse(w.Id.Value, w.ProjectId, w.ParentId, w.Title, w.Description,
            Projects.Domain.Enumerations.WorkItemType.FromId(w.TypeId).Name,
            Projects.Domain.Enumerations.WorkItemStatus.FromId(w.StatusId).Name,
            Projects.Domain.Enumerations.WorkItemPriority.FromId(w.PriorityId).Name,
            Projects.Domain.Enumerations.Criticality.FromId(w.CriticalityId).Name,
            w.OwnerId, w.ResponsibleId, w.ReviewerId, w.DueDate, w.ProgressPercent, w.Tags, w.Deliverables, w.Observations, w.Version, w.UpdatedAt, w.TenantId, w.IsOverdue(DateTime.UtcNow), [] , w.EstimatedHours, w.ActualHours, w.StartedAt, w.ReopenedCount, false);
        return Result.Success(dto);
    }
}