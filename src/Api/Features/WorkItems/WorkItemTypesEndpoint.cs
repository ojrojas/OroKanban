using BuildingBlocks.ServiceDefaults.Endpoints;
using Projects.Domain.Enumerations;

namespace Api.Features.WorkItems;

public sealed class WorkItemTypesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/work-item-types", (HttpContext ctx) =>
        {
            var types = WorkItemType.GetAll().Select(t => new { id = t.Id, name = t.Name }).ToList();
            return Results.Ok(types);
        }).RequireAuthorization();
    }
}
