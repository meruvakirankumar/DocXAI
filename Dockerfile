# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first to leverage Docker layer caching
COPY AutomationEngineService.csproj ./
COPY src/AutomationEngine.Domain/AutomationEngine.Domain.csproj src/AutomationEngine.Domain/
COPY src/AutomationEngine.Application/AutomationEngine.Application.csproj src/AutomationEngine.Application/
COPY src/AutomationEngine.Infrastructure/AutomationEngine.Infrastructure.csproj src/AutomationEngine.Infrastructure/

RUN dotnet restore AutomationEngineService.csproj

# Copy remaining source and build
COPY . .
RUN dotnet build AutomationEngineService.csproj -c Release --no-restore

# ── Stage 2: Publish ──────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish AutomationEngineService.csproj -c Release -o /app/publish /p:UseAppHost=false

# ── Stage 3: Runtime image ────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Cloud Run requires the container to listen on $PORT (default 8080)
ENV PORT=8080
EXPOSE 8080

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AutomationEngineService.dll"]
