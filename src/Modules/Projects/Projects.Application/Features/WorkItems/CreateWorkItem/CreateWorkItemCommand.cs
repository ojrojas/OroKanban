using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Results;

using Microsoft.EntityFrameworkCore;

using Projects.Contracts.Dtos;
using Projects.Domain.Aggregates;
using Projects.Domain.Enumerations;
using Projects.Domain.ValueObjects;
using Projects.Infrastructure.Persistence;

namespace ProjectsApp.Features.WorkItems.CreateWorkItem;

public sealed record CreateWorkItemCommand(Guid ProjectId, Guid TenantId, string Title, string? Description, string Type, string Priority, string Criticality, Guid? ParentId, Guid? OwnerId, Guid? ResponsibleId, Guid? ReviewerId, DateTime? DueDate, decimal EstimatedHours, IReadOnlyList<string>? Tags, IReadOnlyList<string>? Deliverables, int Progress) : ICommand<Result<CreateWorkItemResponse>>;

public sealed class CreateWorkItemValidator : Validator<CreateWorkItemCommand>
{
    public CreateWorkItemValidator()
    {
        RuleFor(x => !string.IsNullOrWhiteSpace(x.Title) && x.Title.Trim().Length >= 1 && x.Title.Trim().Length <= 200, nameof(CreateWorkItemCommand.Title), "Title 1..200");
        RuleFor(x => x.ProjectId != Guid.Empty, nameof(CreateWorkItemCommand.ProjectId), "ProjectId required");
        RuleFor(x => !string.IsNullOrWhiteSpace(x.Type), nameof(CreateWorkItemCommand.Type), "Type required");
    }
}

public sealed class CreateWorkItemHandler(ProjectsDbContext db) : ICommandHandler<CreateWorkItemCommand, Result<CreateWorkItemResponse>>
{
    public async Task<Result<CreateWorkItemResponse>> HandleAsync(CreateWorkItemCommand cmd, CancellationToken ct)
    {
        var projectExists = await db.Projects.AnyAsync(p => p.Id == new Projects.Domain.Ids.ProjectId(cmd.ProjectId) && p.TenantId == cmd.TenantId, ct);
        if (!projectExists) return Error.NotFound("Project.NotFound", "Project not found");

        WorkItemType type;
        try { type = WorkItemType.FromName(cmd.Type); }
        catch { return Error.Validation("WorkItem.InvalidType", $"Unknown WorkItemType {cmd.Type}"); }

        WorkItemPriority priority;
        try { priority = WorkItemPriority.FromName(cmd.Priority); } catch { priority = WorkItemPriority.Medium; }

        Criticality criticality;
        try { criticality = Criticality.FromName(cmd.Criticality); } catch { criticality = Criticality.Medium; }

        // Task/Subtask must have parent WorkItem (Feature/Plan/Issue)
        if (type.Name == WorkItemType.Task.Name || type.Name == WorkItemType.Subtask.Name)
        {
            if (!cmd.ParentId.HasValue) return Error.Validation("WorkItem.ParentRequired", "Task/Subtask debe tener un WorkItem padre (Feature/Plan/Issue)");
        }

        if (cmd.ParentId.HasValue)
        {
            var parent = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == new Projects.Domain.Ids.WorkItemId(cmd.ParentId.Value), ct);
            if (parent is null) return Error.NotFound("WorkItem.ParentNotFound", "Parent not found");
            if (parent.ProjectId != cmd.ProjectId) return Error.Validation("WorkItem.CrossProjectParent", "Parent and child must be in same project");
        }

        // Tags validation & normalization via Tag VO
        var normalizedTags = new List<string>();
        if (cmd.Tags is not null)
        {
            var seen = new HashSet<string>();
            foreach (var t in cmd.Tags)
            {
                try
                {
                    var tag = Tag.Create(t);
                    if (seen.Add(tag.Value)) normalizedTags.Add(tag.Value);
                }
                catch (Exception ex) { return Error.Validation("WorkItem.InvalidTag", ex.Message); }
            }
        }

        // Deliverables (free text 1..200, deduped)
        var normalizedDeliverables = new List<string>();
        if (cmd.Deliverables is not null)
        {
            var seen2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in cmd.Deliverables)
            {
                var v = d?.Trim(); if(string.IsNullOrWhiteSpace(v)) continue;
                if(v.Length>200) return Error.Validation("WorkItem.InvalidDeliverable","Deliverable 1..200");
                if(seen2.Add(v)) normalizedDeliverables.Add(v);
            }
        }

        try { _ = Effort.FromHours(cmd.EstimatedHours); } catch (Exception ex) { return Error.Validation("WorkItem.InvalidEffort", ex.Message); }
        try { _ = ProgressValue.FromPercent(cmd.Progress); } catch (Exception ex) { return Error.Validation("WorkItem.InvalidProgress", ex.Message); }

        var workItem = WorkItem.Create(cmd.TenantId, cmd.ProjectId, cmd.ParentId, cmd.Title, cmd.Description, type.Id, priority.Id, criticality.Id, cmd.OwnerId, cmd.ResponsibleId, cmd.ReviewerId, cmd.DueDate, cmd.EstimatedHours, normalizedTags, cmd.Progress);
        // Persist deliverables at creation (separate from tags)
        if (normalizedDeliverables.Count > 0) workItem.Update(workItem.Title, workItem.Description, priority.Id, criticality.Id, workItem.DueDate, normalizedTags, normalizedDeliverables, workItem.Observations, workItem.ProgressPercent, workItem.EstimatedHours);

        db.WorkItems.Add(workItem);
        await db.SaveChangesAsync(ct);
        // History for creation
        db.WorkItemHistories.Add(new Projects.Domain.Aggregates.WorkItemHistory(workItem.Id.Value, workItem.TenantId, cmd.OwnerId ?? cmd.ResponsibleId, "Created", null, System.Text.Json.JsonSerializer.Serialize(new{ title=workItem.Title, type=type.Name, parentId=workItem.ParentId }), "WorkItem created"));
        await db.SaveChangesAsync(ct);

        var resp = new CreateWorkItemResponse(workItem.Id.Value, workItem.ProjectId, workItem.ParentId, workItem.Title, type.Name, WorkItemStatus.Backlog.Name, priority.Name, criticality.Name, workItem.ResponsibleId, workItem.DueDate, workItem.ProgressPercent, normalizedTags, workItem.Version);
        return Result.Success(resp);
    }
}