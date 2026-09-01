# ServiceDefaults Convention — Foundation (002)

**Spec**: `specs/002-foundation-architecture/spec.md` — FR-006
**Decision**: Research.md Decision 3 — shared-DB/per-schema, plus this convention

Every service in OroKanban MUST call `AddServiceDefaults()` (via `BuildingBlocks.ServiceDefaults`) at host-build time. At foundation stage the sole host is `src/Api`; future module hosts (if ADR-001 later splits the monolith) MUST also call it.

## What `AddServiceDefaults()` wires

- OpenTelemetry: `AddOpenTelemetry()` with AspNetCore, HttpClient, Runtime instrumentation; `UseOtlpExporter()` when `OTEL_EXPORTER_OTLP_ENDPOINT` is set
- Health: `AddHealthChecks().AddCheck("self", Healthy)` → endpoints `MapDefaultEndpoints()` exposes `/health` (all checks) and `/alive` (live-tag only)
- Resilience: `ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler())` (Microsoft.Extensions.Http.Resilience)

## Logging

- Serilog via `BuildingBlocks.Logger` — `SerilogConfigurator` reads `Serilog` section from configuration; sinks Console/File/Loki/Seq per environment

## Where it is called

- `src/Api/Program.cs: builder.AddServiceDefaults()` — before any other service registration

## AppHost propagation

`OroKanban.AppHost/AppHost.cs` does not need explicit OTLP wiring — Aspire injects `OTEL_EXPORTER_OTLP_ENDPOINT` into each `AddProject` automatically. No manual `WithEnvironment("OTEL_...")` required.

## Convention for future modules

If a module becomes a separate host (e.g., `src/Modules/Documents/Documents.Api`), its `Program.cs` MUST also start with `builder.AddServiceDefaults()` and end with `app.MapDefaultEndpoints()`. The architecture test in `tests/Architecture` could be extended to assert this.

## Verification

```bash
curl -s http://localhost:5000/health | jq .
curl -s http://localhost:5000/alive | head
```

Both must respond <1 s; `GetPlatformHealth` (`GET /api/platform/health`) aggregates the same checks plus identity reachability.
