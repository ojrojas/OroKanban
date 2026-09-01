var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure — declare Aspire resources (ADR-001)
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();


postgres.AddDatabase("orokanban");
var identityDb = postgres.AddDatabase("identitydb");

var rabbitmq = builder.AddRabbitMQ("rabbitmq");
var redis = builder.AddRedis("redis");


// ---------------------------------------------------------------------------
// Parámetros / secretos (solo local). En producción se inyectan vía
// `aspire deploy` / variables de entorno del host.
// DataProtection keyring sobrevive entre reinicios vía volumen.
// ---------------------------------------------------------------------------

var symmetricKey = builder.AddParameter("symmetric-security-key", secret: true);
var seedAdminPassword = builder.AddParameter("seed-admin-password", secret: true);


// External identity — consumed, not duplicated (Constitution II, FR-005)
// The oroidentityserver Podman container is external; authority comes via config/endpoint.
IResourceBuilder<ContainerResource> identityServer = builder.AddContainer("identity-api", "localhost/oroidentityserver", "latest")
    // Aspire's https endpoint uses transport=http: the proxy terminates TLS and forwards
    // plaintext HTTP to the container, so the app only needs plain HTTP listeners on 5080
    // and 5086. This annotation makes the proxy use the development certificate.
    .WithHttpsCertificateConfiguration(ctx =>
    {
        ctx.Arguments.Add("--https-certificate-path");
        ctx.Arguments.Add(ctx.PfxPath);
        ctx.EnvironmentVariables.Add("ASPNETCORE_Kestrel__Certificates__Default__Path", ctx.PfxPath);
        ctx.EnvironmentVariables.Add("ASPNETCORE_Kestrel__Certificates__Default__Password", ctx.Password);
        return Task.CompletedTask;
    })
    .WithHttpEndpoint(port: 5080, targetPort: 5080, name: "http")
    .WithHttpsEndpoint(port: 5086, targetPort: 5086, name: "https")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
    // DB aislada: el container debe apuntar a la postgres de Aspire
    .WithReference(identityDb).WaitFor(identityDb)
    // SymmetricSecurityKey ≥32 bytes — compartida por todas las instancias (requerido en prod)
    .WithEnvironment("SymmetricSecurityKey", symmetricKey)
    .WithEnvironment("SEED_TENANT_NAME", "OroMasterTenant")
    .WithEnvironment("SEED_ADMIN_USERNAME", "admin")
    .WithEnvironment("SEED_ADMIN_PASSWORD", seedAdminPassword)
    .WithEnvironment("SEED_ADMIN_EMAIL", "admin@oroclash.local")
    // RabbitMQ opcional (para IntegrationEvents del identity, no para game-state)
    .WithEnvironment("EventBus__RabbitMQ__HostName", rabbitmq.Resource.Name)
    .WithVolume("identity-dp-keys", "/app/data-protection-keys");

// Composition API — scaffolded via `dotnet new webapi` per FR-010
// Uses path-based overload to avoid requiring a marker type from Api.
var api = builder.AddProject("api", "../src/Api/Api.csproj")
    .WithReference(postgres).WaitFor(postgres)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithReference(redis).WaitFor(redis)
    .WaitFor(identityServer)
    .WithEnvironment("Identity__Authority", identityServer.GetEndpoint("http"));

// Angular frontend — scaffolded via `ng new` per FR-010
// Web is an Angular SPA run via `npm start` (dev) or static hosting (prod).
// Declared here as a reference for dashboard visibility; concrete hosting is via `src/Web` dev server.
// (AddNpmApp requires Aspire.Hosting.JavaScript — omitted at foundation stage to keep AppHost minimal.)

builder.Build().Run();
