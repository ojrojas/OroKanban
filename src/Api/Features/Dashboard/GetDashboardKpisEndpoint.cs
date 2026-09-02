using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.ServiceDefaults.Endpoints;

namespace Api.Features.Dashboard;

public sealed class GetDashboardKpisEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard/kpis", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetDashboardKpisQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(detail: result.Error?.Description, statusCode: 500);
        }).RequireAuthorization().WithName("GetDashboardKpis");
    }
}
