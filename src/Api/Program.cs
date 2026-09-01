using Api.Authentication;
using Api.Configuration;
using Api.Tenant;

using BuildingBlocks.CQRS.Behaviors;
using BuildingBlocks.CQRS.DependencyInjection;
using BuildingBlocks.ServiceDefaults;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ServiceDefaults: OTel, health, resilience (FR-006, research Decision via AddServiceDefaults)
builder.AddServiceDefaults();

// Configuration: environment-only, fail-closed (FR-005, contracts/identity-config-contract.md)
// Soporta Oidc:* (EduCore/docs) y Identity:* (compat). Valida que al menos uno tenga Authority/Audience.
builder.Services.AddOptions<IdentityOptions>()
    .Bind(builder.Configuration.GetSection(IdentityOptions.SectionName))
    .BindConfiguration("Identity__") // also bind env-suffixed keys
    .Validate(o =>
    {
        var hasAuthority = !string.IsNullOrWhiteSpace(o.Authority)
            || !string.IsNullOrWhiteSpace(builder.Configuration["Oidc:Authority"])
            || !string.IsNullOrWhiteSpace(builder.Configuration["Oidc__Authority"]);
        var hasAudience = !string.IsNullOrWhiteSpace(o.Audience)
            || !string.IsNullOrWhiteSpace(builder.Configuration["Oidc:Audience"])
            || !string.IsNullOrWhiteSpace(builder.Configuration["Oidc__Audience"]);
        return hasAuthority && hasAudience;
    }, "Oidc:Authority/Audience o Identity:Authority/Audience es requerido. Configura via AppHost (Oidc__*) o appsettings.")
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
builder.Services.AddDbContext<Documents.Infrastructure.Persistence.DocumentsDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("orokanban") ?? "Host=localhost;Port=5432;Database=orokanban;Username=postgres;Password=postgres"));
builder.Services.AddDbContext<AiProcessing.Infrastructure.Persistence.AiProcessingDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("orokanban") ?? "Host=localhost;Port=5432;Database=orokanban;Username=postgres;Password=postgres"));
// AI options (MEAI provider-agnostic) — secrets via env/KeyVault, not source (Principle XIX)
builder.Services.Configure<AiProcessing.Infrastructure.Configuration.AiOptions>(builder.Configuration.GetSection(AiProcessing.Infrastructure.Configuration.AiOptions.SectionName));
builder.Services.Configure<AiProcessing.Infrastructure.Configuration.VectorStoreOptions>(builder.Configuration.GetSection(AiProcessing.Infrastructure.Configuration.VectorStoreOptions.SectionName));

// CQRS — BuildingBlocks canon (no MediatR)
builder.Services.AddCqrs(cqrs => cqrs
    .RegisterHandlersFromAssemblyContaining<Program>()
    .AddOpenBehavior(typeof(LoggingBehavior<,>))
    .AddOpenBehavior(typeof(ValidationBehavior<,>)));

// Endpoints (vertical slices)
builder.Services.AddEndpoints(typeof(Program).Assembly);

// HttpClient for discovery fetch (GetPlatformHealth)
builder.Services.AddHttpClient();

// Auth: validación OpenIddict contra oroidentityserver — patrón solicitado (similar a EduCore)
// Usa Oidc:Authority/Audience/TenantClaim/ClientId/Secret con fallback a Identity:* y mapea claims tenant/role/sub
builder.Services.AddOidcAuthentication(builder.Configuration);

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
        .WithOrigins("http://localhost:4200", "https://localhost:4200", "http://localhost:5000", "https://localhost:5000")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// En Development, permitir cert autofirmado de Aspire para discovery OIDC (https://localhost:5086)
if (builder.Environment.IsDevelopment())
{
    builder.Services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() =>
        new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        }));
}

builder.Services.AddAuthorization(options =>
{
    // Hierarchical policies — all delegate to IAuthorizationEvaluator (subtree + tenant + permission + classification)
    options.AddPolicy("OrganizationManage", p => p.RequireAuthenticatedUser());
    options.AddPolicy("ProjectRead", p => p.RequireAuthenticatedUser());
    options.AddPolicy("WorkItemRead", p => p.RequireAuthenticatedUser());
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