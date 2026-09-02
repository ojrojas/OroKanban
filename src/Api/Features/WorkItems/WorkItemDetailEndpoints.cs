using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.ServiceDefaults.Endpoints;

namespace Api.Features.WorkItems;

public sealed class GetWorkItemDetailEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/work-items/{id:guid}", async (Guid id, HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if (tenantId == Guid.Empty) return Results.Unauthorized();
            var result = await sender.SendAsync(new ProjectsApp.Features.WorkItems.GetWorkItemDetail.GetWorkItemDetailQuery(id, tenantId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}

public sealed class ChangeWorkItemStatusEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/work-items/{id:guid}/status", async (Guid id, HttpContext ctx, ISender sender, ChangeWorkItemStatusRequest body, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if (tenantId == Guid.Empty) return Results.Unauthorized();
            var actorId = ctx.User.FindFirst("sub")?.Value is string sub && Guid.TryParse(sub, out var sg) ? sg : Guid.Empty;
            var result = await sender.SendAsync(new ProjectsApp.Features.WorkItems.ChangeWorkItemStatus.ChangeWorkItemStatusCommand(id, tenantId, body.TargetStatus, body.ExpectedVersion, actorId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}

public sealed record ChangeWorkItemStatusRequest(string TargetStatus, int ExpectedVersion);
