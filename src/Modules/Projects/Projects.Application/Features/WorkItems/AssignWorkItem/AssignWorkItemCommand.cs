using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Results;

using Microsoft.EntityFrameworkCore;

using Projects.Contracts.Dtos;
using Projects.Domain.Services;
using Projects.Infrastructure.Persistence;

namespace ProjectsApp.Features.WorkItems.AssignWorkItem;

public sealed record AssignWorkItemCommand(Guid WorkItemId, Guid AssigneeId, Guid AssignerId, Guid TenantId, Guid ProjectId, int ExpectedVersion) : ICommand<Result<WorkItemDetailResponse>>;

public sealed class AssignWorkItemValidator : Validator<AssignWorkItemCommand>
{
    public AssignWorkItemValidator()
    {
        RuleFor(x => x.WorkItemId != Guid.Empty, nameof(AssignWorkItemCommand.WorkItemId), "WorkItemId required");
        RuleFor(x => x.AssigneeId != Guid.Empty, nameof(AssignWorkItemCommand.AssigneeId), "AssigneeId required");
        RuleFor(x => x.AssignerId != Guid.Empty, nameof(AssignWorkItemCommand.AssignerId), "AssignerId required");
        RuleFor(x => x.ExpectedVersion > 0, nameof(AssignWorkItemCommand.ExpectedVersion), "ExpectedVersion required");
    }
}

public sealed class AssignWorkItemHandler(ProjectsDbContext db, IAssignmentPolicy policy) : ICommandHandler<AssignWorkItemCommand, Result<WorkItemDetailResponse>>
{
    public async Task<Result<WorkItemDetailResponse>> HandleAsync(AssignWorkItemCommand cmd, CancellationToken ct)
    {
        var w = await db.WorkItems.FirstOrDefaultAsync(x => x.Id == new Projects.Domain.Ids.WorkItemId(cmd.WorkItemId) && x.TenantId == cmd.TenantId, ct);
        if (w is null) return Error.NotFound("WorkItem.NotFound", "Work item not found");
        if (w.Version != cmd.ExpectedVersion) return Error.Conflict("WorkItem.Concurrency", "Concurrency conflict");

        var can = await policy.CanAssignAsync(cmd.AssignerId, cmd.AssigneeId, cmd.ProjectId, cmd.TenantId, w.StatusId, ct);
        if (can.IsFailure) return can.Error;

        w.Assign(cmd.AssigneeId, cmd.AssignerId);
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