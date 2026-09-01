using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.ServiceDefaults.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Notifications.Application.Features.MarkRead;

public sealed class MarkReadEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/notifications/{id:guid}/read", async (Guid id, HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var callerId = ctx.User.FindFirst("sub")?.Value is string sub && Guid.TryParse(sub, out var g) ? g : Guid.Empty;
            if (callerId == Guid.Empty) return Results.Unauthorized();
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : (Guid?)null;
            var result = await sender.SendAsync(new MarkReadCommand(id, callerId, tenantId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
