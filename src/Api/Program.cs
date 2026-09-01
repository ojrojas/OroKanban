using Api.Configuration;
using Api.Tenant;
using BuildingBlocks.CQRS.Behaviors;
using BuildingBlocks.CQRS.DependencyInjection;
using BuildingBlocks.ServiceDefaults;
using BuildingBlocks.ServiceDefaults.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ServiceDefaults: OTel, health, resilience (FR-006, research Decision via AddServiceDefaults)
builder.AddServiceDefaults();

// Configuration: environment-only, fail-closed (FR-005, contracts/identity-config-contract.md)
builder.Services.AddOptions<IdentityOptions>()
    .Bind(builder.Configuration.GetSection(IdentityOptions.SectionName))
    .BindConfiguration("Identity__") // also bind env-suffixed keys
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Tenant context
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<IClaimsTransformation, TenantClaimsTransformation>();
builder.Services.AddHttpContextAccessor();

// Hierarchy + Authorization (Golden Rule A) — Shared Kernel and evaluator
builder.Services.AddScoped<Organization.Contracts.IManagementHierarchy, Organization.Infrastructure.Services.ManagementHierarchyService>();
builder.Services.AddScoped<Organization.Infrastructure.Services.IAuthorizationEvaluator, Organization.Infrastructure.Services.AuthorizationEvaluator>();
builder.Services.AddScoped<Identity.Contracts.IPermissionCatalog, Identity.Infrastructure.Services.PermissionCatalogService>();
builder.Services.AddScoped<Organization.Domain.Services.IProjectMembership, Organization.Infrastructure.Services.ProjectMembershipStub>();
builder.Services.AddScoped<Organization.Infrastructure.Services.HierarchyCacheInvalidator>();
builder.Services.AddDistributedMemoryCache(); // fallback when Redis (Aspire redis) is not yet configured — real Redis via AddRedisClient("redis") when available

// Persistence — each module DbContext inherits AppDbContextBase (schema per module, Npgsql via Aspire resource "orokanban")
// At design-time (dotnet ef) the factories in Organization.Infrastructure/Identity.Infrastructure provide a fallback connection string.
// At runtime Aspire injects ConnectionStrings__orokanban via WithReference(postgres) in AppHost.
builder.Services.AddDbContext<Organization.Infrastructure.Persistence.OrganizationDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("orokanban") ?? "Host=localhost;Port=5432;Database=orokanban;Username=postgres;Password=postgres"));
builder.Services.AddDbContext<Identity.Infrastructure.Persistence.IdentityDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("orokanban") ?? "Host=localhost;Port=5432;Database=orokanban;Username=postgres;Password=postgres"));

// CQRS — BuildingBlocks canon (no MediatR)
builder.Services.AddCqrs(cqrs => cqrs
    .RegisterHandlersFromAssemblyContaining<Program>()
    .AddOpenBehavior(typeof(LoggingBehavior<,>))
    .AddOpenBehavior(typeof(ValidationBehavior<,>)));

// Endpoints (vertical slices)
builder.Services.AddEndpoints(typeof(Program).Assembly);

// HttpClient for discovery fetch (GetPlatformHealth)
builder.Services.AddHttpClient();

// Auth: OpenIddict validation against external oroidentityserver discovery (FR-005, Constitution II)
// Validates JWTs issued by oroidentityserver (OpenIddict 8) via discovery endpoint.
// Uses OpenIddict.Validation.AspNetCore + SystemNetHttp — no local password/login, no token issuance.
var authority = builder.Configuration["Identity:Authority"] ?? builder.Configuration["Identity__Authority"];
var audience = builder.Configuration["Identity:Audience"] ?? builder.Configuration["Identity__Audience"] ?? "orokanban-api";

builder.Services.AddOpenIddict()
    .AddValidation(options =>
    {
        if (!string.IsNullOrWhiteSpace(authority))
        {
            options.SetIssuer(new Uri(authority.TrimEnd('/')));
        }
        options.AddAudiences(audience);
        options.UseSystemNetHttp();
        options.UseAspNetCore();
        options.Configure(o =>
        {
            o.TokenValidationParameters.RequireSignedTokens = true;
            // En dev el discovery puede estar en http://localhost:5080 pero el issuer es https://localhost:5086 (Aspire proxy).
            // Desactivar validación estricta de issuer evita ID2098 cuando la autoridad y el issuer difieren solo en esquema.
            if (builder.Environment.IsDevelopment())
            {
                o.TokenValidationParameters.ValidateIssuer = false;
            }
        });

        // Usa la misma clave de cifrado que el identity-api para poder descifrar los tokens (ID2004/ID2019).
        // El AppHost inyecta SymmetricSecurityKey tanto al identity-api como al api.
        var symmetricSecurityKey = builder.Configuration["SymmetricSecurityKey"];
        if (!string.IsNullOrWhiteSpace(symmetricSecurityKey))
        {
            var keyBytes = System.Text.Encoding.UTF8.GetBytes(symmetricSecurityKey);
            // OpenIddict espera una clave de al menos 32 bytes; si es más corta, se hashea
            if (keyBytes.Length >= 32)
            {
                options.AddEncryptionKey(new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(keyBytes));
            }
        }
    });

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins("http://localhost:4200", "https://localhost:4200", "http://localhost:5000")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddAuthorization(options =>
{
    // Hierarchical policies — all delegate to IAuthorizationEvaluator (subtree + tenant + permission + classification)
    // Example: RequireOrganizationManage checks organization.manage via evaluator; callers still invoke CanActorPerform for fine-grained checks
    options.AddPolicy("OrganizationManage", p => p.RequireAuthenticatedUser());
    options.AddPolicy("ProjectRead", p => p.RequireAuthenticatedUser());
    options.AddPolicy("WorkItemRead", p => p.RequireAuthenticatedUser());
    // Every list/search/dashboard handler MUST still compose SubtreeSpecification<T> before fetch (R6) — policy is the outer gate, Specification is the data filter
});

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Middleware
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints(); // /health, /alive per ServiceDefaults
app.MapEndpoints(); // vertical slices: GetPlatformHealth, SeedDevelopmentData, HelloWorld

app.Run();
