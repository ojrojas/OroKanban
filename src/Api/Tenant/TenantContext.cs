namespace Api.Tenant;

public sealed class TenantContext
{
    public string? TenantId { get; set; }
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
}

public sealed class TenantClaimsTransformation : Microsoft.AspNetCore.Authentication.IClaimsTransformation
{
    private readonly TenantContext _tenantContext;

    public TenantClaimsTransformation(TenantContext tenantContext) => _tenantContext = tenantContext;

    public Task<System.Security.Claims.ClaimsPrincipal> TransformAsync(System.Security.Claims.ClaimsPrincipal principal)
    {
        var tenantId = principal.FindFirst("tenant_id")?.Value
                    ?? principal.FindFirst("tenantId")?.Value;
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            _tenantContext.TenantId = tenantId;
        }
        return Task.FromResult(principal);
    }
}
