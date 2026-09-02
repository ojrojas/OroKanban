using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Results;

using Microsoft.EntityFrameworkCore;

using Projects.Contracts.Dtos;
using Projects.Domain.Enumerations;
using Projects.Domain.Services;
using Projects.Infrastructure.Persistence;

namespace ProjectsApp.Features.WorkItems.ChangeWorkItemStatus;

public sealed record ChangeWorkItemStatusCommand(Guid WorkItemId, Guid TenantId, string TargetStatus, int ExpectedVersion, Guid ActorId) : ICommand<Result<WorkItemDetailResponse>>;

public sealed class ChangeWorkItemStatusValidator : Validator<ChangeWorkItemStatusCommand>
{
    public ChangeWorkItemStatusValidator()
    {
        RuleFor(x => x.WorkItemId != Guid.Empty, nameof(ChangeWorkItemStatusCommand.WorkItemId), "WorkItemId required");
        RuleFor(x => !string.IsNullOrWhiteSpace(x.TargetStatus), nameof(ChangeWorkItemStatusCommand.TargetStatus), "TargetStatus required");
        RuleFor(x => x.ExpectedVersion > 0, nameof(ChangeWorkItemStatusCommand.ExpectedVersion), "ExpectedVersion required");
    }
}

public sealed class ChangeWorkItemStatusHandler(ProjectsDbContext db, IWorkItemTransitionPolicy policy) : ICommandHandler<ChangeWorkItemStatusCommand, Result<WorkItemDetailResponse>>
{
    public async Task<Result<WorkItemDetailResponse>> HandleAsync(ChangeWorkItemStatusCommand cmd, CancellationToken ct)
    {
        var w = await db.WorkItems.FirstOrDefaultAsync(x => x.Id.Value == cmd.WorkItemId && x.TenantId == cmd.TenantId, ct);
        if (w is null) return Error.NotFound("WorkItem.NotFound", "Work item not found");
        if (w.Version != cmd.ExpectedVersion) return Error.Conflict("WorkItem.Concurrency", $"Concurrency conflict: expected {cmd.ExpectedVersion} got {w.Version}");

        WorkItemStatus target;
        try { target = WorkItemStatus.FromName(cmd.TargetStatus); }
        catch { return Error.Validation("WorkItem.InvalidStatus", $"Unknown status {cmd.TargetStatus}"); }

        try
        {
            w.ChangeStatus(target.Id, policy, cmd.ActorId);
        }
        catch (BuildingBlocks.Kernel.Domain.Rules.BusinessRuleValidationException ex)
        {
            return Error.Validation("WorkItem.TransitionNotAllowed", ex.Message);
        }

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Error.Conflict("WorkItem.Concurrency", "Concurrency conflict"); }

        var dto = new WorkItemDetailResponse(w.Id.Value, w.ProjectId, w.ParentId, w.Title, w.Description,
            WorkItemType.FromId(w.TypeId).Name,
            WorkItemStatus.FromId(w.StatusId).Name,
            WorkItemPriority.FromId(w.PriorityId).Name,
            Criticality.FromId(w.CriticalityId).Name,
            w.OwnerId, w.ResponsibleId, w.ReviewerId, w.DueDate, w.ProgressPercent, w.Tags, w.Version, w.UpdatedAt, w.TenantId, w.IsOverdue(DateTime.UtcNow), []);
        return Result.Success(dto);
    }
}