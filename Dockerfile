# syntax=docker/dockerfile:1

# Override these to match whatever your Nexus mirror actually serves, e.g.
#   --build-arg DOTNET_SDK_IMAGE=your-registry.internal/dotnet/sdk:8.0
ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0

# ---------------------------------------------------------------------------
# Build stage
# ---------------------------------------------------------------------------
FROM ${DOTNET_SDK_IMAGE} AS build

# NuGet source is never hardcoded (see NuGet.Config) — it must be supplied at
# build time so `dotnet restore` works air-gapped against your Nexus mirror:
#   docker build --build-arg NUGET_SOURCE_URL=https://nexus.internal/repository/nuget-group/index.json .
ARG NUGET_SOURCE_URL
ARG NUGET_SOURCE_USERNAME=""
ARG NUGET_SOURCE_PASSWORD=""
ENV NUGET_SOURCE_URL=${NUGET_SOURCE_URL} \
    NUGET_SOURCE_USERNAME=${NUGET_SOURCE_USERNAME} \
    NUGET_SOURCE_PASSWORD=${NUGET_SOURCE_PASSWORD}

WORKDIR /src

# Copy only dependency-defining files first so `dotnet restore` is cached across
# builds that don't change references/versions. Deliberately restoring only
# Momentum.Api's own dependency graph (itself + SharedKernel + Infrastructure +
# whatever modules it references) — NOT the whole .sln — so a production image
# never needs test-only packages (Testcontainers, xunit, ...) or their sources.
COPY NuGet.Config Directory.Build.props Directory.Packages.props ./
COPY src/Momentum.Api/Momentum.Api.csproj src/Momentum.Api/
COPY src/Momentum.SharedKernel/Momentum.SharedKernel.csproj src/Momentum.SharedKernel/
COPY src/Momentum.Infrastructure/Momentum.Infrastructure.csproj src/Momentum.Infrastructure/
# NOTE: add every new Momentum.Modules.* project's .csproj COPY line here as
# modules land (Phase 2+), alongside the ProjectReference in Momentum.Api.csproj.

RUN dotnet restore src/Momentum.Api/Momentum.Api.csproj

COPY src/ src/

RUN dotnet publish src/Momentum.Api/Momentum.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ---------------------------------------------------------------------------
# Runtime stage — minimal, non-root
# ---------------------------------------------------------------------------
FROM ${DOTNET_RUNTIME_IMAGE} AS final

WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0

# curl is used only for the container HEALTHCHECK below. Requires your OS package
# mirror (not Nexus/NuGet) to be reachable at build time. If it isn't, delete the
# next RUN line and the HEALTHCHECK instruction — orchestrators (k8s, ECS, ...)
# can probe /health/live and /health/ready over HTTP directly without it, and
# docker-compose.yml's api healthcheck would need to be removed too in that case.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

USER $APP_UID

EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=5 \
    CMD curl --fail --silent http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Momentum.Api.dll"]
