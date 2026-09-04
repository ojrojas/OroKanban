using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Projects.Contracts.Dtos;
using Projects.Domain.Aggregates;
using Projects.Domain.Enumerations;
using Projects.Infrastructure.Persistence;
using WorkItemHistoryEntity = Projects.Domain.Aggregates.WorkItemHistory;

namespace ProjectsApp.Features.WorkItems.Deliverables;

public sealed record CreateDeliverableCommand(Guid WorkItemId, Guid TenantId, string Title, string Type, string? Url) : ICommand<Result<DeliverableDto>>;
public sealed record UpdateDeliverableCommand(Guid DeliverableId, Guid TenantId, string Title, string Type, string Status, string? Url) : ICommand<Result<DeliverableDto>>;

public sealed class CreateDeliverableHandler(ProjectsDbContext db) : ICommandHandler<CreateDeliverableCommand, Result<DeliverableDto>>
{
    public async Task<Result<DeliverableDto>> HandleAsync(CreateDeliverableCommand cmd, CancellationToken ct)
    {
        var wid = new Projects.Domain.Ids.WorkItemId(cmd.WorkItemId);
        var w = await db.WorkItems.FirstOrDefaultAsync(x=> x.Id == wid && x.TenantId==cmd.TenantId, ct);
        if(w is null) return Error.NotFound("WorkItem.NotFound","Work item not found");
        DeliverableType type; try{ type = DeliverableType.FromName(cmd.Type);} catch{ return Error.Validation("Deliverable.InvalidType", $"Unknown type {cmd.Type}");}
        if(string.IsNullOrWhiteSpace(cmd.Title) || cmd.Title.Length>200) return Error.Validation("Deliverable.InvalidTitle","Title 1..200");
        var d = new WorkItemDeliverable(Guid.NewGuid(), cmd.WorkItemId, cmd.Title, type.Id, DeliverableStatus.Pending.Id, cmd.Url);
        db.WorkItemDeliverables.Add(d);
        db.WorkItemHistories.Add(new WorkItemHistoryEntity(w.Id.Value, w.TenantId, null, "DeliverableEntity", null, System.Text.Json.JsonSerializer.Serialize(new{ title=cmd.Title, type=type.Name, url=cmd.Url }), "Created deliverable entity"));
        await db.SaveChangesAsync(ct);
        return Result.Success(new DeliverableDto(d.Id, d.WorkItemId, d.Title, type.Name, DeliverableStatus.Pending.Name, d.Url, d.CreatedAt));
    }
}

public sealed class UpdateDeliverableHandler(ProjectsDbContext db) : ICommandHandler<UpdateDeliverableCommand, Result<DeliverableDto>>
{
    public async Task<Result<DeliverableDto>> HandleAsync(UpdateDeliverableCommand cmd, CancellationToken ct)
    {
        var d = await db.WorkItemDeliverables.FirstOrDefaultAsync(x=> x.Id==cmd.DeliverableId, ct);
        if(d is null) return Error.NotFound("Deliverable.NotFound","Deliverable not found");
        DeliverableType type; try{ type = DeliverableType.FromName(cmd.Type);} catch{ return Error.Validation("Deliverable.InvalidType","Unknown type");}
        DeliverableStatus status; try{ status = DeliverableStatus.FromName(cmd.Status);} catch{ return Error.Validation("Deliverable.InvalidStatus","Unknown status");}
        var wid2 = new Projects.Domain.Ids.WorkItemId(d.WorkItemId);
        var w = await db.WorkItems.FirstOrDefaultAsync(x=> x.Id == wid2, ct);
        if(w is not null && w.TenantId!=cmd.TenantId) return Error.Forbidden("Deliverable.Forbidden","Forbidden");
        var before = System.Text.Json.JsonSerializer.Serialize(new{ d.Title, type=DeliverableType.FromId(d.TypeId).Name, status=DeliverableStatus.FromId(d.StatusId).Name, d.Url });
        d.Update(cmd.Title, type.Id, status.Id, cmd.Url);
        var after = System.Text.Json.JsonSerializer.Serialize(new{ title=cmd.Title, type=type.Name, status=status.Name, url=cmd.Url});
        if(w!=null) db.WorkItemHistories.Add(new WorkItemHistoryEntity(w.Id.Value, w.TenantId, null, "DeliverableEntity", before, after, "Updated deliverable entity"));
        await db.SaveChangesAsync(ct);
        return Result.Success(new DeliverableDto(d.Id, d.WorkItemId, d.Title, type.Name, status.Name, d.Url, d.CreatedAt));
    }
}
