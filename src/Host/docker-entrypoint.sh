#!/bin/sh
set -e

APPSETTINGS="/app/wwwroot/appsettings.json"

if [ -n "$API_BASE_URL" ] && [ -f "$APPSETTINGS" ]; then
    sed -i "s|\"ApiBaseUrl\": \"[^\"]*\"|\"ApiBaseUrl\": \"${API_BASE_URL}\"|" "$APPSETTINGS"

    # Blazor precompresses static assets at publish time; if left in place, ASP.NET Core's
    # static file middleware prefers these stale .br/.gz siblings over the file we just edited.
    rm -f "${APPSETTINGS}.br" "${APPSETTINGS}.gz"
fi

exec dotnet Care.Wasm.Host.dll "$@"
