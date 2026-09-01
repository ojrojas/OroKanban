using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Validation;
using OpenIddict.Validation.AspNetCore;

namespace Api.Authentication;

public static class OpenIddictValidationSetup
{
    public static IServiceCollection AddOidcAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Patrón EduCore + docs OpenIddict: Oidc:Authority/Audience son required (fail-fast).
        // Soporta fallback Identity:* para compat, pero sin default silencioso para audience.
        var authority = configuration["Oidc:Authority"] ?? configuration["Identity:Authority"] ?? configuration["Identity__Authority"];
        var audience = configuration["Oidc:Audience"] ?? configuration["Identity:Audience"] ?? configuration["Identity__Audience"];
        var tenantClaim = configuration["Oidc:TenantClaim"] ?? configuration["Identity:TenantClaim"] ?? "tenant_id";
        var clientId = configuration["Oidc:ClientId"] ?? configuration["Identity:ClientId"] ?? configuration["Identity__ClientId"] ?? "orokanban-api";
        var clientSecret = configuration["Oidc:ClientSecret"] ?? configuration["Identity:ClientSecret"] ?? configuration["Identity__ClientSecret"];
        var symmetricSecurityKey = configuration["SymmetricSecurityKey"] ?? configuration["Oidc:SymmetricSecurityKey"] ?? configuration["Identity:SymmetricSecurityKey"];

        // Design-time (dotnet ef) puede no tener authority — no romper migraciones, pero log warn
        var isDesignTime = string.IsNullOrWhiteSpace(authority) && IsDesignTime();
        if (string.IsNullOrWhiteSpace(authority))
        {
            if (isDesignTime)
            {
                // ef design-time: usar placeholder para no romper AddDbContext, token validation no se usa
                authority = "https://localhost:5086";
            }
            else
            {
                throw new InvalidOperationException("'Oidc:Authority' (o 'Identity:Authority') es requerido. Configura Oidc__Authority / Identity__Authority via AppHost o appsettings.");
            }
        }

        if (string.IsNullOrWhiteSpace(audience))
        {
            if (isDesignTime)
            {
                audience = "orokanban-api";
            }
            else
            {
                throw new InvalidOperationException("'Oidc:Audience' (o 'Identity:Audience') es requerido. Configura Oidc__Audience.");
            }
        }

        // Normaliza issuer: doc recomienda string exacto sin Trim/URI wrapping innecesario, pero toleramos trailing slash
        var issuerUri = authority.TrimEnd('/');
        var validIssuers = BuildValidIssuers(issuerUri);

        services.AddOpenIddict()
            .AddValidation(options =>
            {
                options.SetIssuer(issuerUri);
                options.AddAudiences(audience);

                // Introspection solo opt-in para tokens opacos. Docs: para JWT usar discovery (UseSystemNetHttp).
                // Evita ID2146 cuando el token es JWT y UseIntrospection está activo sin necesidad.
                var useIntrospection = configuration["Oidc:UseIntrospection"] ?? configuration["Identity:UseIntrospection"];
                if (!string.IsNullOrEmpty(clientSecret) && string.Equals(useIntrospection, "true", StringComparison.OrdinalIgnoreCase))
                {
                    options.UseIntrospection()
                        .SetClientId(clientId)
                        .SetClientSecret(clientSecret);
                }

                options.UseSystemNetHttp();
                options.UseAspNetCore();

                // Encryption key compartida server<->Api: docs usan Convert.FromBase64String.
                // En dev con DisableAccessTokenEncryption, el access_token es JWT firmado (RS256) no cifrado, por lo que no se necesita AddEncryptionKey.
                // Solo se añade si Oidc:UseEncryption=true (para JWE). Por defecto no se añade para evitar ID2004 con JWT.
                var useEncryption = configuration["Oidc:UseEncryption"] ?? configuration["Identity:UseEncryption"];
                if (!string.IsNullOrWhiteSpace(symmetricSecurityKey) && string.Equals(useEncryption, "true", StringComparison.OrdinalIgnoreCase))
                {
                    var keyBytes = DecodeSymmetricKey(symmetricSecurityKey);
                    if (keyBytes.Length >= 32)
                    {
                        options.AddEncryptionKey(new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(keyBytes));
                    }
                    else
                    {
                        Console.WriteLine($"[OpenIddict] WARN SymmetricSecurityKey decodificada <32 bytes ({keyBytes.Length}) — se ignora. Requiere >=32 bytes base64/utf8.");
                    }
                }

                // Normaliza sub/role/tenant como EduCore JwtBearer OnTokenValidated
                options.AddEventHandler<OpenIddictValidationEvents.ValidateTokenContext>(handler =>
                {
                    handler.UseInlineHandler(context =>
                    {
                        if (context.IsRejected)
                        {
                            Console.WriteLine($"[OpenIddict] ValidateToken rejected: {context.Error} {context.ErrorDescription} ex={context.Exception?.GetType().Name}: {context.Exception?.Message}");
                        }
                        if (context.Principal?.Claims != null)
                        {
                            var mapped = ClaimsPrincipalMapper.MapClaims(context.Principal.Claims, tenantClaim);
                            var authType = context.Principal.Identity?.AuthenticationType ?? OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                            context.Principal = new ClaimsPrincipal(new ClaimsIdentity(mapped, authType));
                        }
                        return ValueTask.CompletedTask;
                    });
                });

                // Loguear errores de proceso para diagnosticar ID2004/IDX*
                options.AddEventHandler<OpenIddictValidationEvents.ProcessAuthenticationContext>(handler =>
                {
                    handler.UseInlineHandler(context =>
                    {
                        if (context.IsRejected)
                        {
                            Console.WriteLine($"[OpenIddict] ProcessAuthentication rejected: {context.Error} {context.ErrorDescription} ex={context.Exception?.Message}");
                            if (context.Exception != null) Console.WriteLine(context.Exception.ToString());
                        }
                        return ValueTask.CompletedTask;
                    });
                });

                // Validación estricta como EduCore + docs: issuer, audience, lifetime, signing key. No deshabilitar en Development.
                options.Configure(o =>
                {
                    o.TokenValidationParameters.RequireSignedTokens = true;
                    o.TokenValidationParameters.ValidateIssuer = true;
                    o.TokenValidationParameters.ValidateAudience = true;
                    o.TokenValidationParameters.ValidateLifetime = true;
                    o.TokenValidationParameters.ValidateIssuerSigningKey = true;
                    o.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(2);
                    // Tolerar localhost <-> 127.0.0.1 y http<->https del proxy Aspire (issuer del discovery puede ser 127.0.0.1:port dinámico)
                    if (validIssuers.Length > 1)
                    {
                        o.TokenValidationParameters.ValidIssuers = validIssuers;
                    }
                });
            });

        services.AddAuthentication(config =>
        {
            config.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            config.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            config.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        });

        return services;
    }

    private static bool IsDesignTime()
    {
        // dotnet ef sets assembly entry o env; heurística simple: EF design-time no tiene ASPNETCORE_ENVIRONMENT=Development con postgres disponible
        var entry = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "";
        return entry.Contains("ef", StringComparison.OrdinalIgnoreCase)
            || Environment.GetEnvironmentVariable("EFCORETOOLS") != null
            || AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name?.StartsWith("Microsoft.EntityFrameworkCore.Design") == true);
    }

    private static string[] BuildValidIssuers(string issuer)
    {
        // Genera variantes para tolerar Aspire proxy dinámico: localhost <-> 127.0.0.1, con/sin port, http/https
        var issuers = new HashSet<string>(StringComparer.Ordinal) { issuer, issuer.TrimEnd('/') + "/" };
        try
        {
            if (Uri.TryCreate(issuer, UriKind.Absolute, out var uri))
            {
                var hostVariants = new[] { "localhost", "127.0.0.1" };
                var schemeVariants = new[] { "https", "http" };
                foreach (var h in hostVariants)
                {
                    foreach (var s in schemeVariants)
                    {
                        var variant = $"{s}://{h}:{uri.Port}";
                        if (uri.PathAndQuery != "/" && !string.IsNullOrWhiteSpace(uri.AbsolutePath))
                            variant += uri.AbsolutePath.TrimEnd('/');
                        issuers.Add(variant);
                        issuers.Add(variant.TrimEnd('/') + "/");
                    }
                }
                // También issuer sin port explícito vs con port
                if (uri.IsDefaultPort)
                {
                    issuers.Add($"{uri.Scheme}://{uri.Host}");
                    issuers.Add($"{uri.Scheme}://{uri.Host}/");
                }
            }
        }
        catch { }
        return issuers.ToArray();
    }

    private static byte[] DecodeSymmetricKey(string key)
    {
        // Docs OpenIddict: Convert.FromBase64String para symmetric. Soporta base64 y fallback UTF8 (para secretos legacy)
        var trimmed = key.Trim();
        try
        {
            // Si es base64 válida y decodifica >=16 bytes, usarla
            var decoded = Convert.FromBase64String(trimmed);
            if (decoded.Length >= 16) return decoded;
        }
        catch { }
        // Fallback: algunos despliegues usan secret plain (hex/utf8) — usar bytes UTF8 si no es base64
        return System.Text.Encoding.UTF8.GetBytes(trimmed);
    }
}
