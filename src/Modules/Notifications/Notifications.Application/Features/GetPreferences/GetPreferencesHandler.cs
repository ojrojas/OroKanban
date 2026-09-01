using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using Notifications.Contracts.Dtos;
using Notifications.Domain.Aggregates;
using Notifications.Domain.Enumerations;
using Notifications.Domain.Services;
using Notifications.Infrastructure.Specifications;

namespace Notifications.Application.Features.GetPreferences;

public sealed class GetPreferencesHandler(IRepository<NotificationPreference, Guid> repo, INotificationPolicy policy) : IQueryHandler<GetPreferencesQuery, Result<PreferencesResponse>>
{
    public async Task<Result<PreferencesResponse>> HandleAsync(GetPreferencesQuery q, CancellationToken ct)
    {
        var spec = new PreferenceByUserSpec(q.UserId);
        var pref = await repo.FirstOrDefaultAsync(spec, ct);
        var raw = pref?.Preferences ?? new Dictionary<int, Dictionary<int, bool>>();
        // Build dictionaries string-keyed for DTO
        var rawStr = ToStringKeyed(raw);
        // Effective = apply policy.IsEnabled for each type/channel
        var rawReadOnly = raw.ToDictionary(k => k.Key, v => (IReadOnlyDictionary<int, bool>)v.Value);
        var effective = new Dictionary<int, Dictionary<int, bool>>();
        foreach (var type in NotificationType.GetAll())
        {
            foreach (var channel in Channel.GetAll())
            {
                var enabled = policy.IsEnabled(type, channel, rawReadOnly);
                if (!effective.ContainsKey(type.Id)) effective[type.Id] = new Dictionary<int, bool>();
                effective[type.Id][channel.Id] = enabled;
            }
        }
        var effectiveStr = ToStringKeyed(effective);
        var mandated = policy.MandatedTypes.Select(t => new MandatedTypeDto(NotificationType.FromId(t.TypeId).Name, t.TypeId, Channel.FromId(t.ChannelId).Name, t.ChannelId)).ToList();
        return Result.Success(new PreferencesResponse(q.UserId, q.TenantId, rawStr, effectiveStr, mandated, new Dictionary<string,bool>{{"InApp", true},{"Email", false}}, pref?.UpdatedAt, pref?.RowVersion != null ? Convert.ToBase64String(pref.RowVersion) : null));
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>> ToStringKeyed(Dictionary<int, Dictionary<int,bool>> src)
    {
        var res = new Dictionary<string, IReadOnlyDictionary<string, bool>>();
        foreach (var outer in src)
        {
            var typeName = NotificationType.GetAll().FirstOrDefault(t => t.Id == outer.Key)?.Name ?? outer.Key.ToString();
            var inner = new Dictionary<string, bool>();
            foreach (var kv in outer.Value)
            {
                var chName = Channel.GetAll().FirstOrDefault(c => c.Id == kv.Key)?.Name ?? kv.Key.ToString();
                inner[chName] = kv.Value;
            }
            res[typeName] = inner;
        }
        return res;
    }
}
