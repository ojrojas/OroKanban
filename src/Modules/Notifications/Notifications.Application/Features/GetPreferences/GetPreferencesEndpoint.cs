using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.ServiceDefaults.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Notifications.Application.Features.GetPreferences;

public sealed class GetPreferencesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/notifications/preferences", async (HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var callerId = ctx.User.FindFirst("sub")?.Value is string sub && Guid.TryParse(sub, out var g) ? g : Guid.Empty;
            if (callerId == Guid.Empty) return Results.Unauthorized();
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            var result = await sender.SendAsync(new GetPreferencesQuery(callerId, tenantId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
