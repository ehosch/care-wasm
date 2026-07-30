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
└── Shared/                 # Still empty — DTOs live in the NSwag-generated client instead
```

## Phase 1 pages (Auth & Users)

- `Pages/Users.razor` (`/users`, `[Authorize(Roles="Admin")]`) — list all
  users, invite (`Components/InviteDialog.razor`), resend/revoke invite,
  change role (blocked for your own row — see gotcha below).
- `Pages/Register.razor` (`/register?token=`, anonymous) — completes an invite.
- `Pages/ForgotPassword.razor` / `Pages/ResetPassword.razor` (anonymous).
- `Layout/NavMenu.razor` — "Users" link wrapped in `<AuthorizeView Roles="Admin">`.

**MudBlazor provider order** (hard MudBlazor 7 requirement, applied from day
one): `MudThemeProvider` → `MudPopoverProvider` → `MudDialogProvider` →
`MudSnackbarProvider`, in `BaseLayout.razor`. `NotFound.razor` omits
`MudDialogProvider` (no dialogs shown there).

**`_Imports.razor` applies `@attribute [Authorize]` globally** — new pages
needing anonymous access must add `@attribute [AllowAnonymous]` explicitly
(currently only `Login.razor`).

## Phase 2 pages (Documents)

- `Pages/Documents.razor` (`/documents`, `[Authorize]`, no role restriction —
  every member can view/download) — `MudTable` listing title, category, file
  name, size, version, uploader, upload date. Download and (if `Version > 1`)
  Version History buttons are visible to everyone; Upload (toolbar) and
  per-row Replace/Delete are wrapped in `<AuthorizeView Roles="Admin">`.
- `Components/UploadDocumentDialog.razor`, `ReplaceDocumentDialog.razor`,
  `VersionHistoryDialog.razor` — the latter lists `DocumentVersionDto` rows
  each with their own download button, calling
  `IDocumentsClient.DownloadVersionAsync`.
- `Layout/NavMenu.razor` — "Documents" link is **not** wrapped in
  `<AuthorizeView Roles="Admin">`, unlike "Users" — deliberate, every
  authenticated member needs to reach it.
- **File download reuses gatekeeper's JS interop Blob pattern** —
  `downloadFileFromStream`/`triggerFileDownload` functions added to
  `wwwroot/index.html` verbatim. A plain `<a href>` can't carry the JWT
  bearer header, so the generated `FileResponse`'s stream is passed to
  `IJSRuntime.InvokeVoidAsync` via `DotNetStreamReference` instead.
- **`AuthorizeView`'s implicit `context` clashes with `MudTable`'s
  `RowTemplate`** — both default to the parameter name `context`, and nesting
  an `<AuthorizeView>` inside a `RowTemplate` (for the Replace/Delete buttons)
  is a Razor compiler error (`RZ9999`) without disambiguation. Fixed with
  `<AuthorizeView Roles="Admin" Context="authContext">` — any future
  `AuthorizeView` nested inside a templated MudBlazor component needs the
  same explicit `Context` attribute.
- **`MudIconButton`'s `Title` attribute triggers analyzer warning `MUD0002`**
  ("Illegal Attribute") in MudBlazor 7. Used `<MudTooltip Text="...">`
  wrapping each icon button instead, to keep the 0-warnings bar this
  codebase holds itself to.

## Phase 3 pages (Care Calendar)

- `Pages/ShiftCalendar.razor` (`/calendar`, `[Authorize]`, no role
  restriction) — prev/this-week/next-week navigation over a plain HTML
  table (Sunday-start, 3 rows for Day/Evening/Overnight × 7 day columns).
  Read-only for everyone; Admins additionally get a per-cell edit icon
  (`<AuthorizeView Roles="Admin" Context="authContext">`, same
  Context-clash fix as the Documents page) opening
  `Components/AssignShiftDialog.razor`.
- `Components/AssignShiftDialog.razor`/`.razor.cs` — a `MudSelect` of active
  users (from `IUsersClient.GetUsersAsync`, filtered `Status == "Active"`)
  plus an "Open (unassigned)" option, calling
  `IShiftsClient.AssignAsync(shiftId, null, new AssignShiftRequest { UserId = ... })`.
- **`MudSelect`'s floating `Label` doesn't shrink reliably with a one-way
  `Value`/`ValueChanged` binding when the selected item's display text is
  long** — with `Label="Assigned to"` and an item text like
  "Unassigned (Open)", the label rendered inline *over* the value instead of
  floating above it, producing visible text like "Assigned to Open)". Fixed
  by dropping the floating `Label` entirely and using a plain `MudText`
  caption above the `MudSelect` instead — more predictable than fighting the
  float-label CSS state for a one-way-bound select.
- The generated `ShiftDto.Date` is `System.DateTimeOffset`, not `DateOnly`
  (see the NSwag gotcha below) — `ShiftCalendar.razor.cs`'s `FindShift`
  compares via `DateOnly.FromDateTime(s.Date.Date) == date`.
- The 7-column grid overflows the viewport at narrower widths with no
  scroll container currently wired up correctly (a wrapping
  `overflow-x: auto` `div` didn't constrain it — likely needs a `min-width`
  fix upstream in `BaseLayout`'s flex container). Known, deferred to
  `CLAUDE_CARE.md`'s Phase 7 "mobile-responsive pass on the calendar view" —
  don't spend time on it before then.

## Phase 4 pages (Self-Assign & Replacement Requests)

- `Pages/ShiftCalendar.razor`/`.razor.cs` — extended with the current
  user's id (`ClaimTypes.NameIdentifier`, same pattern as `Users.razor.cs`'s
  `CurrentUserId`), used to decide per-cell what to show: a "Claim" button
  on any `Open` shift (everyone), a "Request replacement" button when
  `shift.AssignedUserId == currentUserId` and `Status == Assigned`, or a
  "Cancel request" button when `shift.PendingReplacementRequestedByUserId
  == currentUserId` and `Status == ReplacementRequested`. Admin's Edit icon
  is unchanged from Phase 3 and still works in every state as the override
  path.
- `Components/RequestReplacementDialog.razor` — single optional Reason
  field, mirrors `InviteDialog.razor`'s simplicity (inline `@code`, no
  separate code-behind file needed for a dialog this small).
- `Pages/ReplacementQueue.razor`/`.razor.cs` (`/replacement-requests`,
  `[Authorize]`, no role restriction) — `MudTable` of pending requests;
  "Cancel" on the current user's own rows, "Claim" on everyone else's
  (client-side `RequestedByUserId == _currentUserId` check, same idea as
  `Users.razor`'s `context.Id != CurrentUserId` check for hiding your own
  role-editor).
- `Layout/NavMenu.razor` — "Replacement Requests" link added after
  "Calendar", not role-restricted (every member needs to reach it).
- **Debugging the assign/claim/request UI requires actually re-logging-in
  as the target user, not just navigating in a tab that's still holding a
  different user's cached token.** `authToken`/`refreshToken` live in
  `localStorage`, shared per-origin across every tab — opening a second tab
  to "act as Member B" without an explicit fresh login (or a
  `localStorage.clear()` first) silently keeps testing as whoever was last
  logged in on that origin, producing confusing "the button isn't showing"
  false alarms that look like a code bug but aren't. Verify the actually-active
  user by decoding `localStorage.getItem('authToken')`'s JWT payload before
  trusting a test result.

## Phase 5 (Shift Notes)

- `Components/ShiftNotesDialog.razor`/`.razor.cs` (new) — loads
  `IShiftsClient.GetNotesAsync` on open, renders the thread, and posts new
  notes via `AddNoteAsync`, appending the returned `ShiftNoteDto` to the
  local list directly rather than reloading. **Deliberately does not close
  after posting** (unlike every other dialog in this app) — a comment
  thread's natural use is posting more than one note in a visit, so the
  "Post" button stays separate from "Close."
- `Close()` calls `MudDialog.Close(DialogResult.Ok(_anyPosted))` — the
  caller (`ShiftCalendar.razor.cs`'s `OpenNotesDialog`) checks
  `dialogResult is { Canceled: false, Data: true }` before reloading the
  week, so opening a thread and closing it without posting doesn't trigger
  a pointless refetch. This is a different check than the other
  dialogs use (`Canceled: false` alone) precisely because "Close" here
  isn't a cancel/confirm choice — it's the only exit, so the payload
  carries whether anything actually changed.
- `Pages/ShiftCalendar.razor` — every cell (any status) gets a small
  comment-icon button plus a `shift.NoteCount` caption when nonzero, not
  gated by `AuthorizeView` — notes are readable/postable by anyone,
  regardless of admin role or shift assignment.

## Auth

`Client.Infrastructure/Auth/Jwt/JwtAuthenticationService.cs` — JWT-only
`AuthenticationStateProvider` + `IAuthenticationService` + `IAccessTokenProvider`,
modeled on gatekeeper's but with the tenant parameter stripped from
`LoginAsync`/`RefreshAsync` (care-webapi's `TokensController` has no tenant
concept). Caches token/refresh token in local storage via `Blazored.LocalStorage`.
No permissions-claim caching yet (care only has 2 roles — `Admin`/`Member` —
surfaced as a plain `ClaimTypes.Role` claim decoded from the JWT).

**`Auth/Jwt/JwtAuthenticationHeaderHandler.cs` keeps an explicit allowlist of
`[AllowAnonymous]` server routes** (`AnonymousPaths`) that skip the
force-navigate-to-`/login` behavior when no token is cached. Found this the
hard way: the handler originally only excluded `/api/tokens`, so calling
`forgot-password`/`reset-password`/`register` while logged out (no cached
token, which is the normal case for those pages) redirected straight to
`/login` before the anonymous API call ever completed. **Any new
`[AllowAnonymous]` controller action added to care-webapi must be added to
this list too**, or it'll silently misbehave the same way for logged-out
users.

## NSwag client generation

Convention (same as gatekeeper): the generated `ApiClient/CareApi.cs` holds
interface + client class definitions (regenerated wholesale); **hand-written
method extensions/overrides go in a separate `CareApi.Extensions.cs` partial
class** so they survive regeneration — none needed yet, `IUsersClient`'s
generated methods are used as-is.

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

**`nswag.json`'s `dateType` is configured as `System.DateTimeOffset`** — any
care-webapi `DateOnly` property or query parameter (first used in Phase 3's
`ShiftDto.Date`/`weekStart`) generates as `DateTimeOffset` in the client, not
`DateOnly`. Convert with `new DateTimeOffset(dateOnly.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)`
going out and `DateOnly.FromDateTime(dto.Date.Date)` coming back.

**A controller action returning a bare `Ok()`/`IActionResult` with no
`[ProducesResponseType]` generates a `Task<FileResponse>` client method**,
not `Task` — ASP.NET Core's default OpenAPI generation infers an
`application/octet-stream` response for an untyped 200. care-webapi's
`UsersController` adds `[ProducesResponseType(StatusCodes.Status200OK)]` to
every action that just returns `Ok()`, specifically to keep the generated
client methods as plain `Task`. Do the same for any new no-body endpoint.

## Common commands

```bash
dotnet build
dotnet run --project src/Host --launch-profile http   # dev port: 5101
```

`appsettings.Development.json`'s `ApiBaseUrl` points at
`http://localhost:5100/` (care-webapi's **http** dev port), not https —
deliberately, to avoid the self-signed dev cert. A WASM `fetch()` to an
untrusted-cert https origin fails with an opaque `TypeError: Failed to fetch`
in the browser console with no HTTP status to debug from, and Chrome's cert
interstitial isn't scriptable by browser-automation tooling either. If you
need to test the https path specifically, visit the API's https URL directly
first and click through the cert warning to trust it for the session, then
switch `ApiBaseUrl` back.

`src/Host/Properties/launchSettings.json` was originally left at the
template's random auto-generated ports (5162/7034) instead of the documented
5101/7101 — fixed once discovered (via cors.json's allowed origins not
matching what the app was actually running on). If dev login ever fails with
a CORS error, check these two files agree with each other.

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

See `../CLAUDE_CARE.md` for the full phased plan. Phase 0 (scaffold),
Phase 1 (Auth & Users — invite/register/roles/forgot-password), Phase 2
(Documents — upload/replace/delete/version-history), Phase 3 (Care Calendar
Core — week-grid view, admin direct-assign), Phase 4 (Self-Assign &
Replacement Requests — claim, request/cancel/claim replacement, open queue),
and Phase 5 (Shift Notes — per-shift comment thread) are done. Only SMS/email
notification dispatch remains from the original spec.
