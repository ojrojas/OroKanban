using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.ServiceDefaults.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Notifications.Application.Features.UpdatePreferences;

public sealed class UpdatePreferencesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/notifications/preferences", async (UpdatePreferencesRequestDto dto, HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var callerId = ctx.User.FindFirst("sub")?.Value is string sub && Guid.TryParse(sub, out var g) ? g : Guid.Empty;
            if (callerId == Guid.Empty) return Results.Unauthorized();
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            // dto.Preferences is Dictionary<string,Dictionary<string,bool>> — need to map string keys to int ids
            var prefs = new Dictionary<int, Dictionary<int, bool>>();
            foreach (var outer in dto.Preferences)
            {
                int typeId = int.TryParse(outer.Key, out var tidInt) ? tidInt : TryParseType(outer.Key);
                var inner = new Dictionary<int, bool>();
                foreach (var kv in outer.Value)
                {
                    int chId = int.TryParse(kv.Key, out var chInt) ? chInt : TryParseChannel(kv.Key);
                    inner[chId] = kv.Value;
                }
                prefs[typeId] = inner;
            }
            byte[]? rowVersion = dto.RowVersion != null ? Convert.FromBase64String(dto.RowVersion) : null;
            var cmd = new UpdatePreferencesCommand(callerId, tenantId, prefs, rowVersion);
            var result = await sender.SendAsync(cmd, ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }

    private static int TryParseType(string name)
    {
        try { return Notifications.Domain.Enumerations.NotificationType.FromName(name).Id; } catch { return -1; }
    }
    private static int TryParseChannel(string name)
    {
        try { return Notifications.Domain.Enumerations.Channel.FromName(name).Id; } catch { return -1; }
    }
}

public sealed record UpdatePreferencesRequestDto(Dictionary<string, Dictionary<string, bool>> Preferences, string? RowVersion);
