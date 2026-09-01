using System.Diagnostics;
using Api.Tenant;

namespace Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    public const string HeaderName = "X-Correlation-Id";
    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var cid) || !Guid.TryParse(cid, out var guid))
        {
            guid = Guid.NewGuid();
            context.Request.Headers[HeaderName] = guid.ToString();
        }
        context.Items["CorrelationId"] = guid;
        tenantContext.CorrelationId = guid;
        Activity.Current?.SetBaggage("CorrelationId", guid.ToString());
        Activity.Current?.SetTag("correlationId", guid.ToString());
        context.Response.Headers[HeaderName] = guid.ToString();
        await _next(context);
    }
}
