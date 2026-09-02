using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.ServiceDefaults.Endpoints;

namespace Api.Features.Projects;

public sealed class CreateProjectEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/projects", async (HttpContext ctx, ISender sender, CreateProjectRequest body, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if (tenantId == Guid.Empty) return Results.Unauthorized();
            var cmd = new ProjectsApp.Features.ProjectsMgmt.CreateProject.CreateProjectCommand(
                body.Name, body.OwnerId, body.ManagerId, body.Status, body.Priority, body.Criticality,
                body.DueDate, body.Description, tenantId);
            var result = await sender.SendAsync(cmd, ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}

public sealed record CreateProjectRequest(string Name, Guid OwnerId, Guid ManagerId, string Status, string Priority, string Criticality, DateTime? DueDate, string? Description);
