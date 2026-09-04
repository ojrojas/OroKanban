using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.ServiceDefaults.Endpoints;

namespace Api.Features.Projects;

public sealed class GetProjectDetailEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/projects/{id:guid}", async (Guid id, HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if(tenantId==Guid.Empty) return Results.Unauthorized();
            var result = await sender.SendAsync(new ProjectsApp.Features.ProjectsMgmt.GetProjectDetail.GetProjectDetailQuery(id, tenantId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}

public sealed class UpdateProjectEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/projects/{id:guid}", async (Guid id, HttpContext ctx, ISender sender, UpdateProjectRequest body, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if(tenantId==Guid.Empty) return Results.Unauthorized();
            var cmd = new ProjectsApp.Features.ProjectsMgmt.UpdateProject.UpdateProjectCommand(id, tenantId, body.Name, body.Status, body.Priority, body.Criticality, body.DueDate, body.Description);
            var result = await sender.SendAsync(cmd, ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
public sealed record UpdateProjectRequest(string Name, string Status, string Priority, string Criticality, DateTime? DueDate, string? Description);

public sealed class ArchiveProjectEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/projects/{id:guid}/archive", async (Guid id, HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if(tenantId==Guid.Empty) return Results.Unauthorized();
            // load and archive via update command with Archived status
            var get = await sender.SendAsync(new ProjectsApp.Features.ProjectsMgmt.GetProjectDetail.GetProjectDetailQuery(id, tenantId), ct);
            if(!get.IsSuccess) return get.ToHttpResult();
            var p = get.Value!;
            var cmd = new ProjectsApp.Features.ProjectsMgmt.UpdateProject.UpdateProjectCommand(id, tenantId, p.Name, "Archived", p.Priority, p.Criticality, p.DueDate, p.Description);
            var result = await sender.SendAsync(cmd, ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}

public sealed class GetProjectHistoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/projects/{id:guid}/history", async (Guid id, HttpContext ctx, global::Projects.Infrastructure.Persistence.ProjectsDbContext db, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if(tenantId==Guid.Empty) return Results.Unauthorized();
            // simple: return work item histories for project's work items + project audit viaWorkItemHistories? return empty for now with audit fallback
            var histories = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(db.WorkItemHistories.Where(h=> h.TenantId==tenantId).OrderByDescending(h=> h.CreatedAt).Take(50), ct);
            // filter by project workitems
            var workItemIds = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(db.WorkItems.Where(w=> w.ProjectId==id).Select(w=> w.Id.Value), ct);
            var filtered = histories.Where(h=> workItemIds.Contains(h.WorkItemId)).Select(h=> new{ id=h.Id, field=h.Field, from=h.FromJson, to=h.ToJson, comment=h.Comment, timestamp=h.CreatedAt, actorId=h.ActorId}).ToList();
            return Results.Ok(new{ items = filtered });
        }).RequireAuthorization();
    }
}
