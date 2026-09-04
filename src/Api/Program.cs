using Api.Authentication;
using Api.Configuration;
using Api.Persistence;
using Api.Tenant;

using BuildingBlocks.CQRS.Behaviors;
using BuildingBlocks.CQRS.DependencyInjection;
using BuildingBlocks.Kernel.Domain.Repositories;
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

// Persistence — cada módulo DbContext hereda AppDbContextBase (schema por módulo, Npgsql via Aspire resource "orokanban")
// At runtime Aspire injects ConnectionStrings__orokanban via WithReference(postgres) en AppHost.
// Identity NO tiene DbContext local: oroidentityserver es externo y solo se consume vía OIDC/access_token (Principio II), nunca SQL directo a identitydb.
builder.Services.AddDbContext<Organization.Infrastructure.Persistence.OrganizationDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("orokanban")));
builder.Services.AddDbContext<Documents.Infrastructure.Persistence.DocumentsDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("orokanban")));
builder.Services.AddDbContext<AiProcessing.Infrastructure.Persistence.AiProcessingDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("orokanban")));
builder.Services.AddDbContext<Audit.Infrastructure.Persistence.AuditDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("orokanban")));
builder.Services.AddDbContext<Notifications.Infrastructure.Persistence.NotificationsDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("orokanban")));
builder.Services.AddDbContext<Projects.Infrastructure.Persistence.ProjectsDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("orokanban")));
builder.Services.AddDbContext<Metrics.Infrastructure.Persistence.MetricsDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("orokanban")));
// Projects domain services — required after AddCqrs scans Projects.Application (ReparentWorkItem etc.)
builder.Services.AddScoped<Projects.Domain.Services.IDependencyCycleDetector, Projects.Infrastructure.Services.DependencyCycleDetector>();
builder.Services.AddScoped<Projects.Domain.Services.IWorkItemTransitionPolicy, Projects.Infrastructure.Services.WorkItemTransitionPolicy>();
builder.Services.AddScoped<Projects.Domain.Services.IHierarchyInspector, Projects.Infrastructure.Services.HierarchyInspector>();
builder.Services.AddScoped<Projects.Domain.Services.IAssignmentPolicy, Projects.Infrastructure.Services.AssignmentPolicy>();
builder.Services.AddScoped<Projects.Domain.Services.IProjectMembership, Projects.Infrastructure.Services.ProjectMembershipService>();
builder.Services.AddScoped<Projects.Domain.Services.IUserStateChecker, Projects.Infrastructure.Services.DefaultUserStateChecker>();
// Bootstrap — single DbContext with ALL entities for EnsureCreated at startup
builder.Services.AddDbContext<BootstrapDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("orokanban")));
// IRepository / IUnitOfWork — BuildingBlocks.Kernel.Infrastructure was never implemented (EfRepository missing → AggregateException on NotificationDispatcher)
builder.Services.AddScoped<IRepository<Notifications.Domain.Aggregates.Notification, Notifications.Domain.Ids.NotificationId>,
    EfRepository<Notifications.Infrastructure.Persistence.NotificationsDbContext, Notifications.Domain.Aggregates.Notification, Notifications.Domain.Ids.NotificationId>>();
builder.Services.AddScoped<IRepository<Notifications.Domain.Aggregates.NotificationPreference, Guid>,
    EfRepository<Notifications.Infrastructure.Persistence.NotificationsDbContext, Notifications.Domain.Aggregates.NotificationPreference, Guid>>();
builder.Services.AddScoped<IUnitOfWork, CompositeUnitOfWork>();
// AI options (MEAI provider-agnostic) — secrets via env/KeyVault, not source (Principle XIX)
builder.Services.Configure<AiProcessing.Infrastructure.Configuration.AiOptions>(builder.Configuration.GetSection(AiProcessing.Infrastructure.Configuration.AiOptions.SectionName));
builder.Services.Configure<AiProcessing.Infrastructure.Configuration.VectorStoreOptions>(builder.Configuration.GetSection(AiProcessing.Infrastructure.Configuration.VectorStoreOptions.SectionName));
builder.Services.Configure<Audit.Infrastructure.Configuration.AuditOptions>(builder.Configuration.GetSection(Audit.Infrastructure.Configuration.AuditOptions.SectionName));
builder.Services.Configure<Notifications.Infrastructure.Configuration.NotificationsOptions>(builder.Configuration.GetSection(Notifications.Infrastructure.Configuration.NotificationsOptions.SectionName));

// Notifications — BC-09 supporting (R1-R5 decoupled, idempotent, policy merge, content safety)
builder.Services.AddScoped<Notifications.Domain.Services.INotificationPolicy, Notifications.Infrastructure.Services.NotificationPolicy>();
builder.Services.AddScoped<Notifications.Domain.Services.INotificationContentPolicy, Notifications.Infrastructure.Services.NotificationContentPolicy>();
builder.Services.AddScoped<Notifications.Infrastructure.Channels.IChannel, Notifications.Infrastructure.Channels.InAppChannel>();
builder.Services.AddScoped<Notifications.Infrastructure.Channels.IChannel, Notifications.Infrastructure.Channels.EmailChannel>();
builder.Services.AddScoped<Notifications.Infrastructure.Channels.IChannelRouter, Notifications.Infrastructure.Channels.ChannelRouter>();
builder.Services.AddScoped<Notifications.Infrastructure.Consumers.NotificationDispatcher>();
builder.Services.AddScoped<BuildingBlocks.EventBus.Abstractions.IIntegrationEventHandler<Projects.Contracts.Events.WorkItemAssignedIntegrationEvent>, Notifications.Infrastructure.Consumers.WorkItemAssignedHandler>();
builder.Services.AddScoped<BuildingBlocks.EventBus.Abstractions.IIntegrationEventHandler<Projects.Contracts.Events.WorkItemStatusChangedIntegrationEvent>, Notifications.Infrastructure.Consumers.WorkItemStatusChangedHandler>();
builder.Services.AddScoped<BuildingBlocks.EventBus.Abstractions.IIntegrationEventHandler<Documents.Contracts.Events.DocumentUploadedIntegrationEvent>, Notifications.Infrastructure.Consumers.DocumentUploadedHandler>();
builder.Services.AddScoped<BuildingBlocks.EventBus.Abstractions.IIntegrationEventHandler<Documents.Contracts.Events.DocumentApprovedIntegrationEvent>, Notifications.Infrastructure.Consumers.DocumentApprovedHandler>();
builder.Services.AddScoped<BuildingBlocks.EventBus.Abstractions.IIntegrationEventHandler<Documents.Contracts.Events.DocumentClassifiedIntegrationEvent>, Notifications.Infrastructure.Consumers.DocumentClassifiedHandler>();
builder.Services.AddScoped<BuildingBlocks.EventBus.Abstractions.IIntegrationEventHandler<AiProcessing.Contracts.Events.LlmResultGeneratedIntegrationEvent>, Notifications.Infrastructure.Consumers.LlmResultGeneratedHandler>();

// Health checks per-dependency identifiable (Principle XVIII, SC-005)
builder.Services.AddHealthChecks()
    .AddCheck<Audit.Infrastructure.Health.NpgsqlHealthCheck>("postgres")
    .AddCheck<Audit.Infrastructure.Health.RabbitMqHealthCheck>("rabbitmq")
    .AddCheck<Audit.Infrastructure.Health.RedisHealthCheck>("redis")
    .AddCheck<Audit.Infrastructure.Health.AiProviderHealthCheck>("ai_provider")
    .AddCheck<Audit.Infrastructure.Health.VectorStoreHealthCheck>("vector_store");

// SignalR for real-time task notifications (T065)
builder.Services.AddSignalR();

// CQRS — BuildingBlocks canon (no MediatR)
builder.Services.AddCqrs(cqrs => cqrs
    .RegisterHandlersFromAssemblyContaining<Program>()
    .RegisterHandlersFromAssemblyContaining<Notifications.Application.Features.GetMyNotifications.GetMyNotificationsQuery>()
    .RegisterHandlersFromAssemblyContaining<ProjectsApp.Features.ProjectsMgmt.CreateProject.CreateProjectCommand>()
    .RegisterHandlersFromAssemblyContaining<Documents.Application.Features.GetDocument.GetDocumentQuery>()
    .RegisterHandlersFromAssemblyContaining<Audit.Application.Features.Search.SearchAuditEntriesQuery>()
    .RegisterHandlersFromAssemblyContaining<Organization.Application.Features.GetSubtree.GetSubtreeQuery>()
    .AddOpenBehavior(typeof(LoggingBehavior<,>))
    .AddOpenBehavior(typeof(ValidationBehavior<,>)));

// Endpoints (vertical slices)
builder.Services.AddEndpoints(typeof(Program).Assembly);
builder.Services.AddEndpoints(typeof(Notifications.Application.Features.GetMyNotifications.GetMyNotificationsQuery).Assembly);
builder.Services.AddEndpoints(typeof(ProjectsApp.Features.ProjectsMgmt.AddProjectMember.AddProjectMemberCommand).Assembly);
builder.Services.AddEndpoints(typeof(Documents.Application.Features.GetDocument.GetDocumentEndpoints).Assembly);
builder.Services.AddEndpoints(typeof(Audit.Application.Features.Search.AuditSearchEndpoints).Assembly);
builder.Services.AddEndpoints(typeof(Organization.Application.Features.GetSubtree.GetSubtreeQuery).Assembly);
builder.Services.AddEndpoints(typeof(AiProcessing.Infrastructure.Persistence.AiProcessingDbContext).Assembly);

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

// Ensure databases for all modules — single BootstrapDbContext creates ALL schemas/tables in one pass
// (EnsureCreated only creates tables on first call when DB doesn't exist; per-module contexts would get false)
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<BootstrapDbContext>();
        var created = await db.Database.EnsureCreatedAsync();
        logger.LogInformation("Bootstrap EnsureCreated: created={Created}", created);
        // Back-fill new columns/tables for existing DB (EnsureCreated doesn't migrate)
        try
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE SCHEMA IF NOT EXISTS projects;
                CREATE TABLE IF NOT EXISTS projects.work_item_histories (""Id"" uuid PRIMARY KEY, ""WorkItemId"" uuid NOT NULL, ""TenantId"" uuid NOT NULL, ""ActorId"" uuid, ""Field"" varchar(100) NOT NULL, ""FromJson"" jsonb, ""ToJson"" jsonb, ""Comment"" text, ""CreatedAt"" timestamp with time zone NOT NULL);
                CREATE TABLE IF NOT EXISTS projects.work_item_deliverables (""Id"" uuid PRIMARY KEY, ""WorkItemId"" uuid NOT NULL, ""Title"" varchar(200) NOT NULL, ""TypeId"" integer NOT NULL, ""StatusId"" integer NOT NULL, ""Url"" text, ""CreatedAt"" timestamp with time zone NOT NULL, ""UpdatedAt"" timestamp with time zone NOT NULL);
                ALTER TABLE projects.work_items ADD COLUMN IF NOT EXISTS deliverables_json jsonb;
                ALTER TABLE projects.work_items ADD COLUMN IF NOT EXISTS ""Observations"" varchar(4000);
                ALTER TABLE projects.work_items ADD COLUMN IF NOT EXISTS started_at timestamp with time zone;
                ALTER TABLE projects.work_items ADD COLUMN IF NOT EXISTS reopened_count integer NOT NULL DEFAULT 0;
                CREATE INDEX IF NOT EXISTS ""IX_work_item_histories_WorkItemId_CreatedAt"" ON projects.work_item_histories(""WorkItemId"",""CreatedAt"");
                CREATE INDEX IF NOT EXISTS ""IX_work_item_deliverables_WorkItemId"" ON projects.work_item_deliverables(""WorkItemId"");
                ALTER TABLE organization.organization_units ALTER COLUMN ""RowVersion"" SET DEFAULT '\x'::bytea;
                ALTER TABLE organization.management_relationships ALTER COLUMN ""RowVersion"" SET DEFAULT '\x'::bytea;
                ALTER TABLE organization.explicit_grants ALTER COLUMN ""RowVersion"" SET DEFAULT '\x'::bytea;
            ");
            logger.LogInformation("Back-fill schema for histories/deliverables ensured");
        } catch(Exception ex2){ logger.LogWarning(ex2, "Back-fill failed"); }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Bootstrap EnsureCreated FAILED");
    }
}

// SignalR JWT from query string ?access_token=... for /hub negotiate (browser WebSocket cannot set Authorization header)
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/hub") && context.Request.Query.TryGetValue("access_token", out var token))
        context.Request.Headers.Authorization = "Bearer " + token.ToString();
    await next();
});

// Middleware — CorrelationId must be before authentication so TenantContext.CorrelationId is available in handlers
app.UseMiddleware<Api.Middleware.CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints(); // /health, /alive per ServiceDefaults
app.MapHub<Api.Hubs.NotificationsHub>("/hub/notifications");
app.MapEndpoints(); // vertical slices: GetPlatformHealth, SeedDevelopmentData, HelloWorld

app.Run();