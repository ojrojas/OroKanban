using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Notifications.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Api.Features.Notifications;

public sealed class MarkAllReadEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/notifications/mark-all-read", async (HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var callerId = ctx.User.FindFirst("sub")?.Value is string sub && Guid.TryParse(sub, out var g) ? g : Guid.Empty;
            if (callerId == Guid.Empty) return Results.Unauthorized();
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : (Guid?)null;
            var result = await sender.SendAsync(new MarkAllReadCommand(callerId, tenantId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}

public sealed record MarkAllReadCommand(Guid CallerId, Guid? TenantId) : ICommand<Result<int>>;

public sealed class MarkAllReadHandler(NotificationsDbContext db) : ICommandHandler<MarkAllReadCommand, Result<int>>
{
    public async Task<Result<int>> HandleAsync(MarkAllReadCommand cmd, CancellationToken ct)
    {
        var unread = await db.Notifications.Where(n => n.RecipientId == cmd.CallerId && n.ReadAt == null
            && (cmd.TenantId == null || n.TenantId == cmd.TenantId)).ToListAsync(ct);
        foreach (var n in unread) n.MarkRead();
        var count = await db.SaveChangesAsync(ct);
        return Result.Success(count);
    }
}
