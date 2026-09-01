using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.ServiceDefaults.Endpoints;
using Microsoft.AspNetCore.Authentication;

namespace Api.Features.SeedDevelopmentData;

public sealed class SeedDevelopmentDataEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/dev/seed", async (SeedDevelopmentDataCommand cmd, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(cmd, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Problem(detail: result.Error?.Description, statusCode: 400);
        }).WithName("SeedDevelopmentData");
    }
}
