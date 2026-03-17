# Stage 1: Base Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

# Stage 2: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# 1. Copy csproj files for Restore (Optimize Docker Cache)
COPY ["RedactEngine.Worker/RedactEngine.Worker.csproj", "RedactEngine.Worker/"]
COPY ["RedactEngine.Application/RedactEngine.Application.csproj", "RedactEngine.Application/"]
COPY ["RedactEngine.Domain/RedactEngine.Domain.csproj", "RedactEngine.Domain/"]
COPY ["RedactEngine.Infrastructure/RedactEngine.Infrastructure.csproj", "RedactEngine.Infrastructure/"]
COPY ["RedactEngine.Shared/RedactEngine.Shared.csproj", "RedactEngine.Shared/"]
COPY ["RedactEngine.ServiceDefaults/RedactEngine.ServiceDefaults.csproj", "RedactEngine.ServiceDefaults/"]

# 2. Restore dependencies
RUN dotnet restore "./RedactEngine.Worker/RedactEngine.Worker.csproj"

# 3. Copy the rest of the source code
COPY . .

# 4. Build
WORKDIR "/src/RedactEngine.Worker"
RUN dotnet build "./RedactEngine.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Stage 3: Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./RedactEngine.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Stage 4: Final
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RedactEngine.Worker.dll"]
