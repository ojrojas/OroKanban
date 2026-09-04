using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;
using Microsoft.EntityFrameworkCore;
using Projects.Infrastructure.Persistence;

namespace Api.Features.WorkItems;

public sealed class WorkItemChildrenEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/work-items/{id:guid}/children", async (Guid id, HttpContext ctx, ProjectsDbContext db, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if (tenantId == Guid.Empty) return Results.Unauthorized();
            var children = await db.WorkItems.AsNoTracking()
                .Where(w => w.ParentId == id && w.TenantId == tenantId)
                .Select(w => new { w.Id, w.Title, w.TypeId, w.StatusId, w.PriorityId, w.CriticalityId, w.ResponsibleId, w.DueDate, w.EstimatedHours, w.ActualHours, w.ProgressPercent, w.CreatedAt, w.UpdatedAt })
                .ToListAsync(ct);
            return Results.Ok(children.Select(c => new {
                id = c.Id.Value, title = c.Title, type = global::Projects.Domain.Enumerations.WorkItemType.FromId(c.TypeId).Name,
                status = global::Projects.Domain.Enumerations.WorkItemStatus.FromId(c.StatusId).Name,
                priority = global::Projects.Domain.Enumerations.WorkItemPriority.FromId(c.PriorityId).Name,
                criticality = global::Projects.Domain.Enumerations.Criticality.FromId(c.CriticalityId).Name,
                responsibleId = c.ResponsibleId, dueDate = c.DueDate, estimatedHours = c.EstimatedHours, actualHours = c.ActualHours, progress = c.ProgressPercent
            }));
        }).RequireAuthorization();
    }
}

public sealed class WorkItemTimeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/work-items/{id:guid}/time", async (Guid id, HttpContext ctx, ISender sender, RecordTimeRequest body, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if (tenantId == Guid.Empty) return Results.Unauthorized();
            var actorId = ctx.User.FindFirst("sub")?.Value is string sub && Guid.TryParse(sub, out var sg) ? sg : Guid.Empty;
            var result = await sender.SendAsync(new ProjectsApp.Features.WorkItems.RecordTime.RecordTimeCommand(id, tenantId, body.ActualHours, body.Comment, actorId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
public sealed record RecordTimeRequest(decimal ActualHours, string? Comment);

public sealed class EmployeeEffectivenessEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/employees/{userId:guid}/effectiveness", async (Guid userId, HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if (tenantId == Guid.Empty) return Results.Unauthorized();
            var result = await sender.SendAsync(new ProjectsApp.Features.Metrics.GetEmployeeEffectivenessQuery(userId, tenantId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();

        app.MapGet("/api/projects/{projectId:guid}/burnout", async (Guid projectId, HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if (tenantId == Guid.Empty) return Results.Unauthorized();
            var result = await sender.SendAsync(new ProjectsApp.Features.Metrics.GetProjectBurnoutQuery(projectId, tenantId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
