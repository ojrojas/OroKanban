using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.ServiceDefaults.Endpoints;

namespace Api.Features.GetPlatformHealth;

public sealed class GetPlatformHealthEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/platform/health", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetPlatformHealthQuery(), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Problem(detail: result.Error?.Description, statusCode: 500);
        }).WithName("GetPlatformHealth");
    }
}