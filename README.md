# Care Coordination — WASM

A self-hosted Blazor WebAssembly frontend for the Care Coordination project —
a single-tenant app for coordinating family/caregiver support for a patient.

This is the frontend. The .NET 9 Web API backend lives in the companion
[`care-webapi`](https://github.com/ehosch/care-webapi) repository — start
that first, since this app talks to it exclusively via a typed NSwag-generated
client.

## Status

Phase 0 (scaffold), Phase 1 (Auth & Users), and Phase 2 (Documents) are done —
login, a Users page (invite/resend/revoke/change role, Admin only),
self-service registration from an invite link, forgot/reset password, and a
Documents page (upload/replace/delete/version-history for Admins, view and
download for everyone) all work. The shift calendar and notes UI described
in the backend's roadmap don't exist yet. See `care-webapi`'s README for the
default admin login used on first run.

## Quickstart (full stack)

To run this together with the API and a MySQL instance in one step, see
[`care-webapi`'s Quickstart](https://github.com/ehosch/care-webapi#quickstart-full-stack)
— its `docker-compose.full.yml` pulls both published images. Continue below
if you want to run just this frontend on its own (e.g. against an API you're
already running elsewhere).

## Tech stack

- **.NET 9** Blazor WebAssembly (ASP.NET Core hosted)
- **MudBlazor 7** component library, including file upload/download dialogs
- **NSwag**-generated typed API client (`Client.Infrastructure/ApiClient/CareApi.cs`)
- JWT auth via a custom `AuthenticationStateProvider`

## Getting started

### Local development

1. Start `care-webapi` first (see its README) — `dotnet run --project src/Host`.
2. `appsettings.Development.json`'s `ApiBaseUrl` already points at
   `http://localhost:5100/` (care-webapi's default http dev port). Update it
   if you're running the API somewhere else.
3. `dotnet run --project src/Host --launch-profile http` (dev port `5101`).
   Use the `http` profile, not `https` — see Troubleshooting if you need the
   https path specifically.

### Regenerating the API client

After changing anything in `care-webapi`, regenerate the typed client:

```powershell
./scripts/nswag-regen.ps1
```

This requires `care-webapi` to be running locally first. Hand-written client
extensions belong in `CareApi.Extensions.cs` (a separate partial-class file)
so they survive regeneration — never edit the generated `CareApi.cs` directly.

### Docker

```bash
cp .env.example .env   # set API_BASE_URL to your care-webapi's public URL
docker compose up -d --build
```

`API_BASE_URL` is rewritten into the published `wwwroot/appsettings.json` by
a small entrypoint script (`src/Host/docker-entrypoint.sh`) each time the
container **starts** — change it and restart the container, no rebuild
needed. The app listens on host port `5011` by default.

### Pre-built image

```bash
docker run -d -p 5011:8080 \
  -e API_BASE_URL=https://api.yourdomain.example/ \
  ghcr.io/ehosch/care-wasm:latest
```

## Troubleshooting

**`docker pull`/`docker compose pull` fails with `denied: denied` even though
the image is public.** A stale `docker login ghcr.io` credential (e.g. an
expired or insufficiently-scoped personal access token from an unrelated
project) takes precedence over anonymous pull. Fix: `docker logout ghcr.io`,
then retry.

**Login fails with `TypeError: Failed to fetch` in local dev.** `ApiBaseUrl`
points at an `https://` origin with an untrusted self-signed dev certificate.
The browser blocks the request with no usable HTTP status to debug from.
Point `ApiBaseUrl` at care-webapi's **http** dev port instead (the default),
or visit the API's https URL directly first and click through the cert
warning to trust it for the session.

## License

[GNU General Public License v3.0](LICENSE).
