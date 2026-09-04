using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Projects.Contracts.Dtos;
using Projects.Domain.Enumerations;
using Projects.Domain.ValueObjects;
using Projects.Infrastructure.Persistence;
using System.Text.Json;
using WorkItemHistoryEntity = Projects.Domain.Aggregates.WorkItemHistory;

namespace ProjectsApp.Features.WorkItems.UpdateWorkItem;

public sealed record UpdateWorkItemCommand(Guid WorkItemId, Guid TenantId, string Title, string? Description, string Priority, string Criticality, DateTime? DueDate, IReadOnlyList<string>? Tags, IReadOnlyList<string>? Deliverables, string? Observations, int Progress, decimal EstimatedHours, Guid ActorId) : ICommand<Result<WorkItemDetailResponse>>;

public sealed class UpdateWorkItemValidator : Validator<UpdateWorkItemCommand>
{
    public UpdateWorkItemValidator()
    {
        RuleFor(x => x.WorkItemId != Guid.Empty, nameof(UpdateWorkItemCommand.WorkItemId), "WorkItemId required");
        RuleFor(x => !string.IsNullOrWhiteSpace(x.Title) && x.Title.Trim().Length <=200, nameof(UpdateWorkItemCommand.Title), "Title 1..200");
    }
}

public sealed class UpdateWorkItemHandler(ProjectsDbContext db) : ICommandHandler<UpdateWorkItemCommand, Result<WorkItemDetailResponse>>
{
    public async Task<Result<WorkItemDetailResponse>> HandleAsync(UpdateWorkItemCommand cmd, CancellationToken ct)
    {
        var wid = new Projects.Domain.Ids.WorkItemId(cmd.WorkItemId);
        var w = await db.WorkItems.FirstOrDefaultAsync(x=> x.Id == wid && x.TenantId==cmd.TenantId, ct);
        if(w is null) return Error.NotFound("WorkItem.NotFound","Work item not found");
        // Restricción: WorkItem (Feature/Plan/Issue/Epic) solo lo edita su creador (OwnerId)
        var typeName = Projects.Domain.Enumerations.WorkItemType.FromId(w.TypeId).Name;
        var isParentType = typeName == Projects.Domain.Enumerations.WorkItemType.Feature.Name
            || typeName == Projects.Domain.Enumerations.WorkItemType.Plan.Name
            || typeName == Projects.Domain.Enumerations.WorkItemType.Issue.Name
            || typeName == Projects.Domain.Enumerations.WorkItemType.Epic.Name;
        if (isParentType && w.OwnerId.HasValue && w.OwnerId.Value != Guid.Empty && w.OwnerId.Value != cmd.ActorId)
        {
            // Solo el creador (Owner) puede editar el WorkItem padre
            return Error.Forbidden("WorkItem.ForbiddenEdit", "Solo el creador del WorkItem puede editarlo");
        }
        WorkItemPriority priority;
        Criticality criticality;
        try{ priority = WorkItemPriority.FromName(cmd.Priority);} catch{ priority = WorkItemPriority.Medium; }
        try{ criticality = Criticality.FromName(cmd.Criticality);} catch{ criticality = Criticality.Medium; }

        var beforeTags = w.Tags;
        var beforeDeliverables = w.Deliverables;
        var beforeTitle = w.Title;
        var beforeDesc = w.Description;
        var beforePriority = w.PriorityId;
        var beforeCriticality = w.CriticalityId;
        var beforeDue = w.DueDate;
        var beforeObs = w.Observations;

        // normalize tags
        var normalizedTags = new List<string>();
        if(cmd.Tags is not null){
            var seen=new HashSet<string>();
            foreach(var t in cmd.Tags){
                try{ var tag=Tag.Create(t); if(seen.Add(tag.Value)) normalizedTags.Add(tag.Value);} catch(Exception ex){ return Error.Validation("WorkItem.InvalidTag", ex.Message);}
            }
        } else normalizedTags = w.Tags.ToList();

        var normalizedDeliverables = new List<string>();
        if(cmd.Deliverables is not null){
            var seen2=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var d in cmd.Deliverables){
                var v = d?.Trim(); if(string.IsNullOrWhiteSpace(v)) continue;
                if(v.Length>200) return Error.Validation("WorkItem.InvalidDeliverable","Deliverable 1..200");
                if(seen2.Add(v)) normalizedDeliverables.Add(v);
            }
        } else normalizedDeliverables = w.Deliverables.ToList();

        try{ _ = ProgressValue.FromPercent(cmd.Progress);} catch(Exception ex){ return Error.Validation("WorkItem.InvalidProgress", ex.Message);}
        try{ _ = Effort.FromHours(cmd.EstimatedHours);} catch(Exception ex){ return Error.Validation("WorkItem.InvalidEffort", ex.Message);}

        w.Update(cmd.Title, cmd.Description, priority.Id, criticality.Id, cmd.DueDate, normalizedTags, normalizedDeliverables, cmd.Observations, cmd.Progress, cmd.EstimatedHours);

        // history entries granular
        void AddHist(string field, string? from, string? to){
            if(from!=to) db.WorkItemHistories.Add(new WorkItemHistoryEntity(w.Id.Value, w.TenantId, cmd.ActorId, field, from!=null? JsonSerializer.Serialize(from):null, to!=null? JsonSerializer.Serialize(to):null, null));
        }
        AddHist("Title", beforeTitle, cmd.Title);
        AddHist("Description", beforeDesc, cmd.Description);
        AddHist("Priority", WorkItemPriority.FromId(beforePriority).Name, priority.Name);
        AddHist("Criticality", Criticality.FromId(beforeCriticality).Name, criticality.Name);
        AddHist("DueDate", beforeDue?.ToString("o"), cmd.DueDate?.ToString("o"));
        AddHist("Observations", beforeObs, cmd.Observations);
        // tags/deliverables granular diff
        var tagsFrom = JsonSerializer.Serialize(beforeTags);
        var tagsTo = JsonSerializer.Serialize(normalizedTags);
        if(tagsFrom!=tagsTo) db.WorkItemHistories.Add(new WorkItemHistoryEntity(w.Id.Value, w.TenantId, cmd.ActorId, "Tags", tagsFrom, tagsTo, null));
        var delFrom = JsonSerializer.Serialize(beforeDeliverables);
        var delTo = JsonSerializer.Serialize(normalizedDeliverables);
        if(delFrom!=delTo){
            var added = normalizedDeliverables.Except(beforeDeliverables).ToList();
            var removed = beforeDeliverables.Except(normalizedDeliverables).ToList();
            var diff = JsonSerializer.Serialize(new{ added, removed, before=beforeDeliverables, after=normalizedDeliverables});
            db.WorkItemHistories.Add(new WorkItemHistoryEntity(w.Id.Value, w.TenantId, cmd.ActorId, "Deliverables", delFrom, delTo, diff));
        }

        await db.SaveChangesAsync(ct);
        var dto = new WorkItemDetailResponse(w.Id.Value, w.ProjectId, w.ParentId, w.Title, w.Description,
            Projects.Domain.Enumerations.WorkItemType.FromId(w.TypeId).Name,
            WorkItemStatus.FromId(w.StatusId).Name,
            WorkItemPriority.FromId(w.PriorityId).Name,
            Criticality.FromId(w.CriticalityId).Name,
            w.OwnerId, w.ResponsibleId, w.ReviewerId, w.DueDate, w.ProgressPercent, w.Tags, w.Deliverables, w.Observations, w.Version, w.UpdatedAt, w.TenantId, w.IsOverdue(DateTime.UtcNow), [] , w.EstimatedHours, w.ActualHours, w.StartedAt, w.ReopenedCount, false);
        return Result.Success(dto);
    }
}
