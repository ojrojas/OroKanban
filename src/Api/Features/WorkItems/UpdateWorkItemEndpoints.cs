using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.ServiceDefaults.Endpoints;
using Microsoft.EntityFrameworkCore;
using ProjectsDbContext = global::Projects.Infrastructure.Persistence.ProjectsDbContext;

namespace Api.Features.WorkItems;

public sealed class UpdateWorkItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/work-items/{id:guid}", async (Guid id, HttpContext ctx, ISender sender, UpdateWorkItemRequest body, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if(tenantId==Guid.Empty) return Results.Unauthorized();
            var actorId = ctx.User.FindFirst("sub")?.Value is string sub && Guid.TryParse(sub, out var sg) ? sg : Guid.Empty;
            var cmd = new ProjectsApp.Features.WorkItems.UpdateWorkItem.UpdateWorkItemCommand(id, tenantId, body.Title, body.Description, body.Priority, body.Criticality, body.DueDate, body.Tags, body.Deliverables, body.Observations, body.Progress, body.EstimatedHours, actorId);
            var result = await sender.SendAsync(cmd, ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
public sealed record UpdateWorkItemRequest(string Title, string? Description, string Priority, string Criticality, DateTime? DueDate, IReadOnlyList<string>? Tags, IReadOnlyList<string>? Deliverables, string? Observations, int Progress, decimal EstimatedHours);

public sealed class GetWorkItemHistoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/work-items/{id:guid}/history", async (Guid id, HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if(tenantId==Guid.Empty) return Results.Unauthorized();
            var result = await sender.SendAsync(new ProjectsApp.Features.WorkItems.HistoryQueries.GetWorkItemHistoryQuery(id, tenantId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}

public sealed class DeliverableEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/work-items/{id:guid}/deliverables", async (Guid id, HttpContext ctx, ProjectsDbContext db, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if(tenantId==Guid.Empty) return Results.Unauthorized();
            var wid = new global::Projects.Domain.Ids.WorkItemId(id);
            var w = await db.WorkItems.FirstOrDefaultAsync(x=> x.Id == wid && x.TenantId==tenantId, ct);
            if(w is null) return Results.NotFound();
            var list = await db.WorkItemDeliverables.Where(d=> d.WorkItemId==id).ToListAsync(ct);
            var dto = list.Select(d=> new global::Projects.Contracts.Dtos.DeliverableDto(d.Id, d.WorkItemId, d.Title, global::Projects.Domain.Enumerations.DeliverableType.FromId(d.TypeId).Name, global::Projects.Domain.Enumerations.DeliverableStatus.FromId(d.StatusId).Name, d.Url, d.CreatedAt)).ToList();
            return Results.Ok(dto);
        }).RequireAuthorization();

        app.MapPost("/api/work-items/{id:guid}/deliverables", async (Guid id, HttpContext ctx, ISender sender, CreateDeliverableRequest body, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if(tenantId==Guid.Empty) return Results.Unauthorized();
            var result = await sender.SendAsync(new ProjectsApp.Features.WorkItems.Deliverables.CreateDeliverableCommand(id, tenantId, body.Title, body.Type, body.Url), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();

        app.MapPut("/api/deliverables/{id:guid}", async (Guid id, HttpContext ctx, ISender sender, UpdateDeliverableRequest body, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if(tenantId==Guid.Empty) return Results.Unauthorized();
            var result = await sender.SendAsync(new ProjectsApp.Features.WorkItems.Deliverables.UpdateDeliverableCommand(id, tenantId, body.Title, body.Type, body.Status, body.Url), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
public sealed record CreateDeliverableRequest(string Title, string Type, string? Url);
public sealed record UpdateDeliverableRequest(string Title, string Type, string Status, string? Url);

public sealed class CreateWorkItemWrappedEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/projects/{projectId:guid}/work-items", async (Guid projectId, HttpContext ctx, ISender sender, CreateWorkItemBody body, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if(tenantId==Guid.Empty) return Results.Unauthorized();
            var cmd = new ProjectsApp.Features.WorkItems.CreateWorkItem.CreateWorkItemCommand(projectId, tenantId, body.Title, body.Description, body.Type ?? "Task", body.Priority ?? "Medium", body.Criticality ?? "Medium", body.ParentId, body.OwnerId, body.ResponsibleId, body.ReviewerId, body.DueDate, body.EstimatedHours ?? 0, body.Tags, body.Tags, body.Progress ?? 0);
            var result = await sender.SendAsync(cmd, ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
public sealed record CreateWorkItemBody(string Title, string? Description, string? Type, string? Priority, string? Criticality, Guid? ParentId, Guid? OwnerId, Guid? ResponsibleId, Guid? ReviewerId, DateTime? DueDate, decimal? EstimatedHours, IReadOnlyList<string>? Tags, int? Progress);
