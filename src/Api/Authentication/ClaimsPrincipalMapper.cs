using System.Security.Claims;

namespace Api.Authentication;

public static class ClaimsPrincipalMapper
{
    public static IEnumerable<Claim> MapClaims(IEnumerable<Claim> claims, string tenantClaim)
    {
        var list = claims.ToList();
        var mapped = new List<Claim>(list.Count);

        foreach (var claim in list)
        {
            // Normaliza sub, role y tenant para que queden consistentes con JwtBearer OnTokenValidated anterior
            if (claim.Type == "sub" || claim.Type == ClaimTypes.NameIdentifier)
            {
                mapped.Add(new Claim(ClaimTypes.NameIdentifier, claim.Value, claim.ValueType, claim.Issuer));
                mapped.Add(new Claim("sub", claim.Value, claim.ValueType, claim.Issuer));
            }
            else if (claim.Type == "roles" || claim.Type == "role")
            {
                mapped.Add(new Claim(ClaimTypes.Role, claim.Value, claim.ValueType, claim.Issuer));
                mapped.Add(new Claim("role", claim.Value, claim.ValueType, claim.Issuer));
            }
            else if (claim.Type == tenantClaim)
            {
                mapped.Add(new Claim("tenant_id", claim.Value, claim.ValueType, claim.Issuer));
                mapped.Add(new Claim(tenantClaim, claim.Value, claim.ValueType, claim.Issuer));
            }
            else
            {
                mapped.Add(claim);
            }
        }

        return mapped;
    }
}