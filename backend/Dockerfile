# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=10.0

# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# Copy project definition files for cached layer restore
COPY Finora.slnx ./
COPY src/Finora.Domain/Finora.Domain.csproj src/Finora.Domain/
COPY src/Finora.Application/Finora.Application.csproj src/Finora.Application/
COPY src/Finora.Infrastructure/Finora.Infrastructure.csproj src/Finora.Infrastructure/
COPY src/Finora.Api/Finora.Api.csproj src/Finora.Api/
COPY tests/Finora.Domain.Tests/Finora.Domain.Tests.csproj tests/Finora.Domain.Tests/
COPY tests/Finora.Integration.Tests/Finora.Integration.Tests.csproj tests/Finora.Integration.Tests/

RUN dotnet restore Finora.slnx

# Copy full source and test projects
COPY . .

# Execute tests during build to guarantee zero-defect deployment artifacts
RUN dotnet test tests/Finora.Domain.Tests/Finora.Domain.Tests.csproj -c Release --no-restore

# Publish optimized API binary
WORKDIR /src/src/Finora.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

# Install curl for container health check
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Run as non-root user for enhanced security
USER $APP_UID

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:5000 \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 5000

HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "Finora.Api.dll"]

