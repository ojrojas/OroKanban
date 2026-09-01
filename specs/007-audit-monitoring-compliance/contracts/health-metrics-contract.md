# Contract: Health, Metrics, Logs, and Correlation

**Module**: `Audit` (monitoring) + platform `ServiceDefaults` (BuildingBlocks.ServiceDefaults) | **Base path**: `/health`, `/alive`, `/metrics` (Prometheus) | **Auth**: `GET /health` is anonymous? no — `health.read` via `IAuthorizationEvaluator` or `AllowAnonymous` for readiness probe (per Aspire `AddServiceDefaults`, `MapDefaultEndpoints` is anonymous for liveness/readiness). | **Conventions**: OTel `ActivitySource` `OroKanban.Api`, `Meter` `OroKanban.Metrics`, `Serilog` OTLP to Aspire dashboard + backends (alerts via ADR-007-02).

---

## GET /health — Readiness (per-dependency identifiable)

**Handler**: `HealthCheckService` via `AddHealthChecks` registrations + `MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse, Predicate = _ => true })`

```json
// Response 200 Healthy (all entries Healthy) or 503 Unhealthy (any Unhealthy)
// Application/json from UIResponseWriter:
{
  "status": "Healthy | Unhealthy | Degraded",
  "totalDuration": "00:00:00.1234567",
  "entries": {
    "postgres": { "status": "Healthy", "description": "Npgsql connection succeeded", "duration": "00:00:00.012", "data": {} },
    "rabbitmq": { "status": "Healthy", "description": "RabbitMQ broker reachable", "data": {} },
    "redis": { "status": "Healthy", "data": {} },
    "ai_provider": { "status": "Healthy", "data": { "model": "gpt-4o-2024-08-06" } },
    "vector_store": { "status": "Healthy", "data": {} }
  }
}
// When postgres down (Npgsql SocketException):
{
  "status": "Unhealthy",
  "entries": {
    "postgres": { "status": "Unhealthy", "description": "Npgsql.SocketException: *** (ConnectionString masked)", "exception": "Npgsql.SocketException: ***", "data": {} },
    "rabbitmq": { "status": "Healthy" },
    "redis": { "status": "Healthy" },
    "ai_provider": { "status": "Healthy" },
    "vector_store": { "status": "Healthy" }
  }
}
// Distinguishable per dependency: HealthReport.Entries["postgres"] Unhealthy while Entries["rabbitmq"] Healthy — not aggregated 503 alone (SC-005). Each entry's Exception.Message is ***-masked for secrets (ConnectionString→***, ApiKey→***).
// Health checks registered:
//   AddHealthChecks()
//     .AddCheck<NpgsqlHealthCheck>("postgres", HealthStatus.Unhealthy, tags: new[] { "ready", "db" })
//     .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: new[] { "ready", "messaging" })
//     .AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready", "cache" })
//     .AddCheck<AiProviderHealthCheck>("ai_provider", tags: new[] { "ready", "ai" })
//     .AddCheck<VectorStoreHealthCheck>("vector_store", tags: new[] { "ready", "vector" })
// Each implements IHealthCheck with timeout 5s, HealthStatus.Unhealthy on exception. Architecture test asserts 5 distinct IHealthCheck registrations (AddCheck count ==5).
// Errors: 503 Unhealthy (not 500) when any entry Unhealthy — HTTP status is health status, not 500. 401 not applicable (AllowAnonymous for readiness probe, but health page itself does not leak audit data).
```

**Per-dependency health check implementations** (each in `Audit.Infrastructure/Health` or `Api/Health`):

```csharp
public sealed class NpgsqlHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext ctx, CancellationToken ct)
    {
        try { await using var conn = await dataSource.OpenConnectionAsync(ct); await using var cmd = new NpgsqlCommand("SELECT 1", conn); await cmd.ExecuteScalarAsync(ct); return HealthCheckResult.Healthy("Npgsql connection succeeded"); }
        catch (Exception ex) { return HealthCheckResult.Unhealthy("Npgsql.SocketException: ***", ex); } // ex.Message masked for ConnectionString
    }
}
// Similarly RabbitMqHealthCheck (RabbitMQ.Client CreateConnection), RedisHealthCheck (StackExchangeRedis PingAsync), AiProviderHealthCheck (IChatClient.GetResponseAsync("ping") with Temperature=0), VectorStoreHealthCheck (VectorStore count query)
```

---

## GET /alive — Liveness (no dependency checks)

```json
// Mapped as MapHealthChecks("/alive", new HealthCheckOptions { Predicate = _ => false }) or AddCheck("self") only
// Response 200 Healthy always unless process dead (no postgres/rabbitmq check):
{
  "status": "Healthy",
  "entries": {
    "self": { "status": "Healthy", "description": "Process alive" }
  }
}
// Liveness vs readiness split: /health fails when postgres down but /alive still Healthy (process not dead) — verified by HealthPerDependencyTests.
```

---

## GET /metrics — Prometheus Metrics (via `AddServiceDefaults` `MeterProvider` + `PrometheusExporter`)

**Metrics** (`Meter` `OroKanban.Metrics` via `System.Diagnostics.Metrics`):

```text
# HELP http_requests_total Total HTTP requests (Counter<long> tags: endpoint, tenantId, method)
http_requests_total{endpoint="QueueLlmOperation",tenantId="guid",method="POST"} 100

# HELP http_requests_failed_total Failed HTTP requests (Counter<long> tags: endpoint, status, tenantId) — R5 authorization failures
http_requests_failed_total{endpoint="QueueLlmOperation",status="403",tenantId="guid"} 4
http_requests_failed_total{endpoint="GetDocument",status="500",tenantId="guid"} 1

# HELP job_failed_total Failed background jobs (Counter<long> tags: job, stage, tenantId)
job_failed_total{job="document_processing",stage="VirusScan",tenantId="guid"} 2
job_failed_total{job="ai_processing",stage="Embedding",tenantId="guid"} 1

# HELP rabbitmq_queue_depth Current RabbitMQ queue depth (ObservableGauge<long> tags: queue)
rabbitmq_queue_depth{queue="ai.processing.embedding"} 5
rabbitmq_queue_depth{queue="document.processing.validation"} 0

# HELP http_request_duration_ms HTTP request latency (Histogram<double> tags: endpoint) — R5 latency
http_request_duration_ms_bucket{endpoint="QueueLlmOperation",le="100"} 50
http_request_duration_ms_bucket{endpoint="QueueLlmOperation",le="300"} 95
http_request_duration_ms_bucket{endpoint="QueueLlmOperation",le="500"} 99

# HELP db_errors_total DB errors (Counter<long> tags: operation, tenantId)
db_errors_total{operation="Npgsql",tenantId="guid"} 1

# HELP health_check_status Health check status (ObservableGauge<int> tags: check)
health_check_status{check="postgres"} 1  # 1 Healthy, 0 Unhealthy
```

**Scraping**: `Aspire` `AddServiceDefaults()` registers `AddOpenTelemetry().WithMetrics(m => m.AddMeter("OroKanban.Metrics").AddAspNetCoreInstrumentation().AddRuntimeInstrumentation().AddNpgsqlInstrumentation()).WithTracing(t => t.AddSource("OroKanban.Api").AddAspNetCoreInstrumentation())` → `PrometheusExporter` on `/metrics` or OTLP exporter to `Aspire` dashboard `Metrics` page (no code change). Aspire dashboard `Metrics` shows `QueueDepth` gauge for `document_processing` and `ai_processing` topics, `Latency` histogram for `QueueLlmOperation` p95 <300ms. Alerts are ADR (not infra): `ADR-007-02` chooses `Prometheus AlertManager` vs `Grafana` vs OTel `alerts` topic — this spec only emits metrics, not alerting rules.

---

## Logs + Traces + Correlation (R3, R5, SC-004)

**Logs** (`Serilog` via `BuildingBlocks.Logger` `AddSerilog` + OTLP to `Aspire` dashboard `Logs` + backends `Seq/Loki` via `Serilog.Sinks.Grafana.Loki` if ADR-007-02 chooses):

```text
[INF] HTTP POST /api/documents 202 CorrelationId=guid TenantId=guid Actor=guid (traceId=..., spanId=...)
[INF] DocumentUploadedIntegrationEvent published EventId=guid CorrelationId=guid (spanId=...)
[INF] AuditEventConsumer processed EventId=guid AuditId=guid CorrelationId=guid Action=DocumentUploaded Result=Success
[WRN] AuthorizationDenied DocumentId=guid Actor=guid CorrelationId=guid Result=Denied (also AuditEntry with Action=AuthorizationDenied)
[ERR] Npgsql.SocketException: *** CorrelationId=guid (DB error also metric db_errors_total)
```

**Traces** (OTel `ActivitySource` `OroKanban.Api` + `Activity.Baggage` `CorrelationId`):

```text
TraceId=guid (same as CorrelationId for audit's distributed workflow, but OTel TraceId is separate W3C trace-id — correlation is via Baggage "CorrelationId")
Span: HTTP POST /api/documents (Activity Baggage: CorrelationId=guid, TenantId=guid)
  -> Span: DomainEvent DocumentUploaded (Baggage CorrelationId)
    -> Span: IntegrationEvent DocumentUploadedIntegrationEvent (Id=guid, CorrelationId=guid)
      -> Span: AuditEventConsumer (AuditId=guid, CorrelationId=guid, Action=DocumentUploaded)
        -> Span: AuditDbContext SaveChanges (AuditEntry AuditId=guid)
```

**CorrelationId propagation**: `CorrelationIdMiddleware` at `Api Program.cs` before `UseAuthentication`: `if (!Headers.TryGetValue("X-Correlation-Id", out var cid) || !Guid.TryParse(cid, out var guid)) guid=Guid.NewGuid(); Activity.Current?.SetBaggage("CorrelationId", guid.ToString()); TenantContext.CorrelationId=guid; Response.Headers["X-Correlation-Id"]=guid.ToString();` — every `DomainEvent.CorrelationId` (via `Activity.Baggage`) and `IntegrationEvent.CorrelationId` (via `TenantContext.CorrelationId` in `IOutboxWriter`) and `AuditEntry.CorrelationId` (via consumer `IntegrationEvent.CorrelationId ?? Activity.Baggage`) equals the originating HTTP `X-Correlation-Id` (or generated). `GetOperationTimeline` query does `WHERE audit.CorrelationId=@cid ORDER BY Timestamp asc` to reconstruct 7-entry timeline (HTTP→storage→processing→LLM→review).

**OTel backends**: `Aspire` `AddServiceDefaults()` console OTLP already surfaces in Aspire dashboard `Traces`/`Metrics`/`Logs` (no code change). `Seq`/`Loki` optional later per `ADR-007-02` (dashboard choice) — this spec only emits OTel, not the backend infra itself (per Out-of-Scope, backend is ADR).

