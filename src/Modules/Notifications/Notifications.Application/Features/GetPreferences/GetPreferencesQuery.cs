using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
using Notifications.Contracts.Dtos;

namespace Notifications.Application.Features.GetPreferences;

public sealed record GetPreferencesQuery(Guid UserId, Guid TenantId) : IQuery<Result<PreferencesResponse>>;
