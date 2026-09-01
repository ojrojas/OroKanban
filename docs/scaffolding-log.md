# Scaffolding Log — Foundation and Architecture (002)

**Feature**: 002-foundation-architecture | **Spec**: specs/002-foundation-architecture/spec.md | **Date**: 2026-08-31
**FR-010**: All new applications MUST be created via platform-defined CLI commands. This log records the exact commands + tool versions for reproducibility.

## Tool Versions

- `dotnet --version`: 10.0.400 (from `global.json`)
- `ng version` (Angular CLI): 22.1.6 / Node 24.20.0 / npm 11.19.0
- `Aspire.AppHost.Sdk`: 13.5.3 (from `OroKanban.AppHost/OroKanban.AppHost.csproj`)

## Commands Executed (FR-010)

### Composition API

```bash
dotnet new webapi -n Api -o src/Api -f net10.0
# then: delete WeatherForecast sample files, add references to BuildingBlocks.*
```

### Angular Frontend

```bash
npx @angular/cli@latest new orokanban-web --directory src/Web --routing --style=scss --skip-git --package-manager npm
# adapted afterwards to minimal-ui-design-system tokens + ngrx-signal-store skeleton per plan Project Structure
```

### Bounded-Context Modules (9 × 4 layers)

```bash
# per module <Module> in Identity, Organization, Projects, Metrics, Documents, AiProcessing, Search, Audit, Notifications:
dotnet new classlib -n <Module>.Domain -o src/Modules/<Module>/<Module>.Domain -f net10.0
dotnet new classlib -n <Module>.Application -o src/Modules/<Module>/<Module>.Application -f net10.0
dotnet new classlib -n <Module>.Infrastructure -o src/Modules/<Module>/<Module>.Infrastructure -f net10.0
dotnet new classlib -n <Module>.Contracts -o src/Modules/<Module>/<Module>.Contracts -f net10.0
# then wire: Domain ← Application/Infrastructure; Contracts standalone; all added to OroKanban.slnx via `dotnet sln add`
```

### Architecture Guard

```bash
dotnet new classlib -n Architecture -o tests/Architecture -f net10.0
# converted to xUnit: <IsTestProject>true</IsTestProject> + PackageReference xunit, Microsoft.NET.Test.Sdk, NetArchTest.Rules (or reflection fallback)
```

### AppHost Resources (no scaffold — declarative in AppHost.cs)

No `dotnet new aspire-*` scaffold needed beyond the existing `Aspire.AppHost.Sdk` project; resources are declared in code `OroKanban.AppHost/AppHost.cs` per research Decision 2.

## Notes

- All `dotnet new` invocations use `-f net10.0` to satisfy `global.json` 10.0.400 and constitution Principle III.
- No manual folder/file creation for new projects — FR-010 prohibits it. Deletions are only the template sample files (WeatherForecast).
- This log is appended with final `dotnet --version` + `ng version` output at the end of implementation (T034).

## Final Verification (T034) — 2026-08-31T19:50:28-05:00

```
dotnet --version: 10.0.400
ng version:
Angular CLI       : 22.1.6
Node.js           : 24.20.0
Package Manager   : npm 11.19.0
```

All 35 tasks (T001–T035) completed. Build: `dotnet build OroKanban.slnx -warnaserror` 0 warnings. Tests: `dotnet test tests/Architecture` 8 passed.
