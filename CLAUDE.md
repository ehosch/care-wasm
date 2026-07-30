# CLAUDE.md — care-wasm

.NET 9 Blazor WebAssembly (hosted) frontend for the Care Coordination site.
Scaffolded fresh but modeled on `gatekeeper-wasm-net9`'s architecture and
MudBlazor 7 conventions, single-tenant (no tenant parameter on login/refresh).
Full product spec: `../CLAUDE_CARE.md`. Companion backend: `../care-webapi/`.

## Architecture

```
src/
├── Host/                  # ASP.NET Core host serving the WASM app (WebAssembly.Server)
├── Client/                # Blazor WASM app — Layout/ (BaseLayout, MainLayout, NotFound, NavMenu), Pages/
├── Client.Infrastructure/  # NSwag-generated API client, JWT auth (AuthenticationStateProvider), MudBlazor DI
└── Shared/                 # Empty for now — will hold DTOs synced from care-webapi once Phase 1 adds them
```

**MudBlazor provider order** (hard MudBlazor 7 requirement, applied from day
one): `MudThemeProvider` → `MudPopoverProvider` → `MudDialogProvider` →
`MudSnackbarProvider`, in `BaseLayout.razor`. `NotFound.razor` omits
`MudDialogProvider` (no dialogs shown there).

**`_Imports.razor` applies `@attribute [Authorize]` globally** — new pages
needing anonymous access must add `@attribute [AllowAnonymous]` explicitly
(currently only `Login.razor`).

## Auth

`Client.Infrastructure/Auth/Jwt/JwtAuthenticationService.cs` — JWT-only
`AuthenticationStateProvider` + `IAuthenticationService` + `IAccessTokenProvider`,
modeled on gatekeeper's but with the tenant parameter stripped from
`LoginAsync`/`RefreshAsync` (care-webapi's `TokensController` has no tenant
concept). Caches token/refresh token in local storage via `Blazored.LocalStorage`.
No permissions-claim caching yet (care only has 2 roles — `Admin`/`Member` —
surfaced as a plain `ClaimTypes.Role` claim decoded from the JWT).

## NSwag client generation

Convention (same as gatekeeper): the generated `ApiClient/CareApi.cs` holds
interface + client class definitions (regenerated wholesale); **hand-written
method extensions/overrides go in a separate `CareApi.Extensions.cs` partial
class** so they survive regeneration — none exist yet since no controllers
beyond `TokensController` exist in care-webapi.

```powershell
# 1. Start care-webapi first (dotnet run --project src/Host, or dockerized)
./scripts/nswag-regen.ps1
```

`ApiClient/nswag.json` points at `https://localhost:7100/swagger/v1/swagger.json`
(care-webapi's dev https port). **Two package-version gotchas hit during
Phase 0 scaffolding, fixed in this repo — don't reintroduce them if you copy
this csproj/nswag.json pattern elsewhere:**
- `NSwag.MSBuild` 14.1.0 only ships `NSwagExe_Net60` / `NSwagExe_Net80`
  MSBuild properties — **not** `NSwagExe_Net90` (gatekeeper's own csproj
  references the nonexistent `Net90` variable, which silently no-ops instead
  of failing loudly). `Client.Infrastructure.csproj`'s `NSwag` target uses
  `$(NSwagExe_Net80)` — this runs fine even in a .NET 9 project as long as the
  .NET 8 shared runtime is installed (it's a standalone codegen tool, not tied
  to the target project's TFM).
- `nswag.json`'s `"runtime"` value must be a member of NSwag's `Runtime` enum
  for the installed `NSwag.MSBuild` version — `"Net90"` throws a
  `JsonSerializationException` at generation time. Use `"Net80"`.

Generated client methods take a leading `string? api_version` parameter (from
`Asp.Versioning`'s URL substitution) — always pass `null` unless a specific
version is required, e.g. `_tokensClient.GetTokenAsync(null, request)`.

## Common commands

```bash
dotnet build
dotnet run --project src/Host          # dev ports: http 5101, https 7101
```

## Docker

```bash
cp .env.example .env   # set API_BASE_URL to care-webapi's public URL
docker compose up -d --build
```

| Container | Host Port |
|---|---|
| `care-wasm` | 5011 → 8080 |

`API_BASE_URL` is rewritten into the published `wwwroot/appsettings.json` at
**container startup** by `src/Host/docker-entrypoint.sh` — a deliberate
deviation from `gatekeeper-wasm-net9`'s build-time `sed` pattern, adopted so
the public `ghcr.io` image is actually reusable by someone who just
`docker run`s it with their own `API_BASE_URL` rather than needing to rebuild
from source. The entrypoint also deletes the stale precompressed
`.br`/`.gz` siblings of `appsettings.json` after rewriting it — ASP.NET
Core's static file middleware prefers those over the plain file if left in
place, silently serving the old baked-in URL.

## Roadmap

See `../CLAUDE_CARE.md` for the full phased plan. This scaffold (Phase 0) has
only a login page and an empty home page — no shift calendar, documents, or
notes UI yet.
