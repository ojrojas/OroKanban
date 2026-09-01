namespace Notifications.Contracts.Dtos;

public sealed record MandatedTypeDto(string Type, int TypeId, string Channel, int ChannelId);

public sealed record PreferencesResponse(
    Guid UserId,
    Guid TenantId,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>> RawPreferences,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>> EffectivePreferences,
    IReadOnlyList<MandatedTypeDto> MandatedTypes,
    IReadOnlyDictionary<string, bool> DefaultForNewUser,
    DateTime? UpdatedAt,
    string? RowVersion);

public sealed record UpdatePreferencesRequest(
    Dictionary<string, Dictionary<string, bool>> Preferences,
    string? RowVersion);
