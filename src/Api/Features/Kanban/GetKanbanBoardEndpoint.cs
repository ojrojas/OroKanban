using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.ServiceDefaults.Endpoints;

namespace Api.Features.Kanban;

public sealed class GetKanbanBoardEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/projects/{projectId:guid}/board", async (Guid projectId, HttpContext ctx, ISender sender,
            string? status, Guid? assignee, Guid? epic, string? priority, string? criticality, string? tags,
            string? swimlane, string? sort, string? sortDir, int? page, int? pageSize, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if (tenantId == Guid.Empty) return Results.Unauthorized();
            var query = new ProjectsApp.Features.WorkItems.Queries.GetKanbanBoardQuery(
                projectId, tenantId, status, assignee, epic, priority, criticality, tags, swimlane, sort, sortDir, page ?? 1, pageSize ?? 20);
            var result = await sender.SendAsync(query, ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
