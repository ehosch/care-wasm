FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Host/Care.Wasm.Host.csproj", "src/Host/"]
COPY ["src/Client/Care.Wasm.Client.csproj", "src/Client/"]
COPY ["src/Client.Infrastructure/Care.Wasm.Client.Infrastructure.csproj", "src/Client.Infrastructure/"]
COPY ["src/Shared/Care.Wasm.Shared.csproj", "src/Shared/"]
RUN dotnet restore "src/Host/Care.Wasm.Host.csproj"
COPY . .
WORKDIR "/src/src/Host"
RUN dotnet build "Care.Wasm.Host.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Care.Wasm.Host.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY src/Host/docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh
# ApiBaseUrl defaults to whatever's committed in wwwroot/appsettings.json;
# set API_BASE_URL at `docker run`/compose time to override it without rebuilding.
ENTRYPOINT ["/docker-entrypoint.sh"]
