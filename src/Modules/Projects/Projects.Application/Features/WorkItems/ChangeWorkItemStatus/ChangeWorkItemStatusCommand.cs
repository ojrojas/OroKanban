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
        var w = await db.WorkItems.FirstOrDefaultAsync(x => x.Id == new Projects.Domain.Ids.WorkItemId(cmd.WorkItemId) && x.TenantId == cmd.TenantId, ct);
        if (w is null) return Error.NotFound("WorkItem.NotFound", "Work item not found");
        if (w.Version != cmd.ExpectedVersion) return Error.Conflict("WorkItem.Concurrency", $"Concurrency conflict: expected {cmd.ExpectedVersion} got {w.Version}");

        WorkItemStatus target;
        try { target = WorkItemStatus.FromName(cmd.TargetStatus); }
        catch { return Error.Validation("WorkItem.InvalidStatus", $"Unknown status {cmd.TargetStatus}"); }

        // Regla de cierre jerárquico: WorkItem (Feature/Plan/Issue) no puede cerrarse si tiene Tasks hijas abertas
        if (target.Id == WorkItemStatus.Completed.Id)
        {
            var hasOpenChildren = await db.WorkItems.AnyAsync(x => x.ParentId == w.Id.Value && x.StatusId != WorkItemStatus.Completed.Id, ct);
            if (hasOpenChildren)
                return Error.Validation("WorkItem.ChildrenNotCompleted", "Todas las Tasks hijas deben estar Completed antes de cerrar el WorkItem");
        }

        var fromName = WorkItemStatus.FromId(w.StatusId).Name;
        try
        {
            w.ChangeStatus(target.Id, policy, cmd.ActorId);
        }
        catch (BuildingBlocks.Kernel.Domain.Rules.BusinessRuleValidationException ex)
        {
            return Error.Validation("WorkItem.TransitionNotAllowed", ex.Message);
        }
        db.WorkItemHistories.Add(new global::Projects.Domain.Aggregates.WorkItemHistory(w.Id.Value, w.TenantId, cmd.ActorId, "Status", System.Text.Json.JsonSerializer.Serialize(fromName), System.Text.Json.JsonSerializer.Serialize(target.Name), null));

        // Reapertura automática del padre si un hijo se reabre y el padre estaba cerrado
        if (w.ParentId.HasValue && target.Id != WorkItemStatus.Completed.Id)
        {
            var parent = await db.WorkItems.FirstOrDefaultAsync(x => x.Id == new Projects.Domain.Ids.WorkItemId(w.ParentId.Value) && x.TenantId == cmd.TenantId, ct);
            if (parent != null && parent.StatusId == WorkItemStatus.Completed.Id)
            {
                // Si una Task hija se reabre, el WorkItem vuelve a InProgress y el reloj sigue
                try { parent.ChangeStatus(WorkItemStatus.InProgress.Id, policy, cmd.ActorId); }
                catch { /* ignora si transición no permitida (ej. Completed→InProgress no permitido desde otro estado) */ }
                db.WorkItemHistories.Add(new global::Projects.Domain.Aggregates.WorkItemHistory(parent.Id.Value, parent.TenantId, cmd.ActorId, "ReopenedByChild", System.Text.Json.JsonSerializer.Serialize(parent.StatusId), System.Text.Json.JsonSerializer.Serialize(parent.StatusId), "Reopened because child " + w.Id.Value + " was reopened"));
            }
        }

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Error.Conflict("WorkItem.Concurrency", "Concurrency conflict"); }

        var dto = new WorkItemDetailResponse(w.Id.Value, w.ProjectId, w.ParentId, w.Title, w.Description,
            WorkItemType.FromId(w.TypeId).Name,
            WorkItemStatus.FromId(w.StatusId).Name,
            WorkItemPriority.FromId(w.PriorityId).Name,
            Criticality.FromId(w.CriticalityId).Name,
            w.OwnerId, w.ResponsibleId, w.ReviewerId, w.DueDate, w.ProgressPercent, w.Tags, w.Deliverables, w.Observations, w.Version, w.UpdatedAt, w.TenantId, w.IsOverdue(DateTime.UtcNow), [] , w.EstimatedHours, w.ActualHours, w.StartedAt, w.ReopenedCount, false);
        return Result.Success(dto);
    }
}