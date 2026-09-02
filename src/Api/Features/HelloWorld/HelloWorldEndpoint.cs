using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.ServiceDefaults.Endpoints;

namespace Api.Features.HelloWorld;

public sealed class HelloWorldEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/hello", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new HelloWorldQuery(), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Problem(detail: result.Error.Description, statusCode: 401);
        })
        .RequireAuthorization() // ← validates token via OpenIddict discovery (oroidentityserver)
        .WithName("HelloWorld")
        .WithSummary("Hello World — requires a valid Bearer token from oroidentityserver")
        .WithDescription("Use `Authorization: Bearer <access_token>` where the token is obtained from `POST {Identity__Authority}/connect/token` (authorization_code / password flow) or via the Angular Web login. Returns the caller's `sub`, `tenant_id`, `roles` and server time to confirm the integration works.");
    }
}