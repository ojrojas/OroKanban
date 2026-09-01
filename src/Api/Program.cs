using Api.Configuration;
using Api.Tenant;
using BuildingBlocks.CQRS.Behaviors;
using BuildingBlocks.CQRS.DependencyInjection;
using BuildingBlocks.ServiceDefaults;
using BuildingBlocks.ServiceDefaults.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

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

// CQRS — BuildingBlocks canon (no MediatR)
builder.Services.AddCqrs(cqrs => cqrs
    .RegisterHandlersFromAssemblyContaining<Program>()
    .AddOpenBehavior(typeof(LoggingBehavior<,>))
    .AddOpenBehavior(typeof(ValidationBehavior<,>)));

// Endpoints (vertical slices)
builder.Services.AddEndpoints(typeof(Program).Assembly);

// HttpClient for discovery fetch (GetPlatformHealth)
builder.Services.AddHttpClient();

// Auth: JWT bearer against external oroidentityserver discovery (FR-005)
var authority = builder.Configuration["Identity:Authority"] ?? builder.Configuration["Identity__Authority"];
var audience = builder.Configuration["Identity:Audience"] ?? builder.Configuration["Identity__Audience"] ?? "orokanban-api";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (!string.IsNullOrWhiteSpace(authority))
        {
            options.Authority = authority.TrimEnd('/');
            options.RequireHttpsMetadata = false; // allow http for local Podman container
        }
        options.Audience = audience;
        options.TokenValidationParameters.ValidateAudience = !string.IsNullOrWhiteSpace(audience);
        // Fail-closed is via ValidateOnStart above + RequireAuthenticatedUser on protected endpoints;
        // missing Authority means the handler will reject all tokens (401) — startup still succeeds so health remains observable.
    });

builder.Services.AddAuthorization();

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Middleware
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints(); // /health, /alive per ServiceDefaults
app.MapEndpoints(); // vertical slices: GetPlatformHealth, SeedDevelopmentData

app.Run();
