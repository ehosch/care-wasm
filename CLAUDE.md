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

**Superseded by Phase 9** — the 3-row Day/Evening/Overnight table,
`AssignShiftDialog`, and the per-cell "Claim" button described below no
longer exist; see Phase 9's section for the current 24-hour grid. Left
here for history/context.

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
- **Fixed in Phase 7**: the 7-column grid used to overflow the viewport at
  narrower widths instead of scrolling within its own `overflow-x: auto`
  wrapper `div`. Root cause was `Layout/MainLayout.razor`'s `MudContainer`
  (inside `MudMainContent`/`MudLayout`'s flex layout) having no
  `min-width: 0` — flex items default to `min-width: auto`, so a wide child
  can't be compressed by its ancestor, and the whole page stretches instead
  of the child scrolling. Fix was one line: `Style="min-width: 0;"` on that
  `MudContainer` — global, not calendar-specific, so it also applies to any
  future wide content on any page. Verified by injecting a same-origin
  `<iframe>` sized ~390px wide into a logged-in tab (the browser-automation
  `resize_window` tool didn't actually shrink this environment's Chrome
  window — an iframe gets its own independent viewport for layout purposes,
  which works just as well for testing this kind of flex/overflow bug).

## Phase 4 pages (Self-Assign & Replacement Requests)

**The "Claim" button described below no longer exists** (Phase 9 — claiming
uncovered time is now just clicking the uncovered cell and saving); the
Request-replacement/Cancel-request behavior is unchanged.

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

## Phase 6 (Notifications)

Notification dispatch itself is entirely backend (Hangfire-enqueued email +
SMS) — no dedicated frontend page. The only frontend work is collecting a
phone number:

- `Pages/Register.razor` — optional "Phone number (optional, for SMS
  notifications)" field bound to the generated `RegisterRequest.PhoneNumber`,
  same `EditForm`/`_model` pattern already used for Name/Password.
- `Pages/Users.razor`/`.razor.cs` — new Phone column showing `"—"` when
  null, with a small edit icon opening `Components/EditPhoneNumberDialog.razor`
  (single field, mirrors `InviteDialog.razor`'s inline-`@code` simplicity)
  that calls the new `IUsersClient.UpdatePhoneNumberAsync`. No extra
  `AuthorizeView` needed — the whole page is already `[Authorize(Roles =
  "Admin")]`, and (unlike role-changing) there's no "can't edit your own"
  restriction on phone numbers.

## Invite via SMS

`Components/InviteDialog.razor` gained a second, optional "Phone number
(optional, sends the invite via text too)" `MudTextField` alongside Email,
passed through as `CreateInviteRequest.PhoneNumber`. If left blank, the
invite stays email-only (unchanged Phase 1 behavior) — the backend decides
whether to also enqueue an SMS based on whether a phone number came
through. No other frontend changes: the Users page's existing Phone
column/edit dialog (Phase 6) already reflects the invite-time number
immediately, since it's set on the `ApplicationUser` row the moment the
invite is created, before the invitee ever registers.

## Friendly error messages

`Client.Infrastructure/ApiClient/ApiErrorHelper.cs` (hand-written, lives
alongside the generated client so it survives NSwag regen) parses
`ApiException.Response` as JSON and handles both error shapes care-webapi
actually produces: ASP.NET Core's built-in `{"errors": {"Field": ["msg"]}}`
(model validation) and this app's own `ExceptionMiddleware` shape
`{"statusCode":N,"message":"..."}` — falling back to a generic "Something
went wrong" if neither parses. Every `catch (ApiException ex)` block across
the app calls `ApiErrorHelper.GetFriendlyMessage(ex)` instead of
`ex.Message` (which, unfixed, would show the raw JSON body plus a
`traceId` — `ex.Message` is literally `message + "\n\nStatus:
...\nResponse: \n" + rawJsonBody` in the NSwag-generated `ApiException`).
`Login.razor.cs` catches the broader `Exception`, not `ApiException` (a
fallback for network errors, since `AuthService.LoginAsync` already
returns `false` for a bad password) — `ApiErrorHelper` has a second
`GetFriendlyMessage(Exception)` overload for that call site.

## Calendar color coding, times, and adjustable shift boundaries (Phase 8)

**Fully superseded by Phase 9** — the slider-based `AssignShiftDialog`
described here was deleted entirely (not just modified) and replaced by
direct click-to-toggle editing on the calendar grid. Nothing in this
section reflects the current code; see Phase 9's section below.

## Phase 9 — blockless calendar (24-hour grid, click-to-toggle editing)

There are no more shift types. A day is just 24 fillable hourly blocks;
nothing is a "shift" until someone claims or is assigned a contiguous run
of them.

- `Pages/ShiftCalendar.razor`/`.razor.cs` — a plain HTML table, 24 rows
  (hour 0-23) × 7 day columns, replacing the old 3-row Day/Evening/Overnight
  table. Row height is a compact ~22px to address the old layout's wasted
  vertical space. `GetCellInfo(day, hour)` determines what a given
  (day, hour) cell shows by scanning the loaded `_shifts` for absolute
  interval overlap (same `GetAbsoluteStart`/`GetAbsoluteEnd` math as
  `care-webapi`'s `ShiftService.cs`, ported client-side) — there's no more
  `(Date, ShiftType)` lookup since shifts aren't typed. A cell is one of
  three kinds: `Uncovered` (light gray, clickable to start a new shift),
  `Shift` (green/amber by `Status`), or `Pending` (blue — the shift
  currently being created or resized, see below). **Known accepted
  limitation, same precedent as Phase 8's old `GapAfterMinutes` week-edge
  behavior**: a shift starting the day before the displayed week isn't
  loaded, so its bleed into the first day's early-morning hours won't
  render as covered.
- **No more per-shift edit icon** — clicking anywhere on an editable
  shift's cells (Admin, or the shift's own assignee — `CanEdit(shift)`)
  directly enters edit mode for that shift; clicking an uncovered cell
  starts a brand-new pending selection. Buttons that live *inside* a
  shift's cell (notes icon, Request/Cancel replacement, the Admin-only
  Reassign icon) each wrap their `MudButton`/`MudIconButton` in a plain
  `<span @onclick:stopPropagation="true">` — **`@onclick:stopPropagation`
  cannot be applied directly to a MudBlazor component**, since MudBlazor
  components take a `OnClick` `EventCallback` parameter, not a raw
  `@onclick` attribute, and the directive's codegen collides with it
  (`RZ10010: the component parameter 'OnClick' is used two or more
  times`). Wrapping in a plain HTML element is the fix — don't try to put
  `@onclick:stopPropagation` on a `MudButton`/`MudIconButton` itself again.
- **Editing model — only the 1-2 cells adjacent to the pending range's
  current boundary are ever clickable**, growing or shrinking it by
  exactly one hour per click (`HandleCellClick`): clicking the range's
  current first/last hour shrinks it (clearing the whole pending
  selection entirely if that would leave zero duration — same as
  Cancel); clicking the cell just outside the first/last hour grows it.
  This keeps the range contiguous by construction with no separate
  validation step. All of this is local component state
  (`_pendingStart`/`_pendingEnd`/`_isCreating`/`_editingShiftId`) — no API
  calls happen until Save.
- An inline `MudPaper` action bar (not a modal dialog) appears above the
  grid whenever a pending range exists, showing the formatted range, an
  assignee picker (only interactive for Admins — non-admins are locked to
  themselves) when creating, and Save/Cancel/(Admin-only Delete) buttons.
  Save calls `POST /api/shifts` (creating) or `PUT /{id}/times` (resizing
  an existing shift); Delete calls `DELETE /{id}` after a plain
  `DialogService.ShowMessageBox` confirm.
- **Full-absorb courtesy confirm is entirely client-side** — before
  Save, if the pending range would fully swallow an existing shift
  (checkable locally since the week's shifts are already loaded),
  `DialogService.ShowMessageBox` asks to confirm, naming the
  about-to-be-removed shift's assignee. There is no backend equivalent of
  this anymore (see care-webapi's Phase 9 gotchas) — this is purely a
  frontend nicety, not enforced server-side.
- **`Components/AssignShiftDialog` is gone, replaced by
  `Components/ReassignShiftDialog.razor`/`.razor.cs`** — just the
  assignee `MudSelect` (Admin-only entry point via the small
  `SwapHoriz`-icon button in a shift's cell), calling
  `IShiftsClient.AssignAsync`. No more sliders, no more `AdjustTimesAsync`
  call in this dialog — time editing lives entirely in the grid now.
- **The old `ClaimAsync`/"Claim" button is gone** — claiming previously-
  uncovered time is just clicking the uncovered cell(s) and saving with
  yourself as the assignee, same unified flow as any other creation.
- NSwag regen **was** required for this phase (unlike Phase 8) — the DTO
  shapes changed meaningfully (`ShiftType` dropped, `CreateShiftRequest`
  added, `AssignShiftRequest`/`AdjustShiftTimesRequest` reshaped,
  `GapAfterMinutes` dropped). Already regenerated as part of this phase;
  future changes to the shift endpoints will need the same regen
  workaround documented below.

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

See `../CLAUDE_CARE.md` for the full phased plan. Every phase is done,
including Phase 7's mobile-responsive calendar fix. Phase 8 (post-go-live,
driven by real production use) added friendly validation error messages;
its slider-based calendar UI was then fully replaced by Phase 9's
blockless 24-hour grid (no more fixed shift types — see the "Phase 9"
section above for the current model). Only the actual homelab deployment
of this latest work remains — an operational step on the user's own
infrastructure, not a code change tracked here.
