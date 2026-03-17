# Stage 1: Base Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Stage 2: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
ENV ASPNETCORE_HTTP_PORTS=8080

# 1. Copy csproj files for Restore (Optimize Docker Cache)
COPY ["RedactEngine.ApiService/RedactEngine.ApiService.csproj", "RedactEngine.ApiService/"]
COPY ["RedactEngine.Application/RedactEngine.Application.csproj", "RedactEngine.Application/"]
COPY ["RedactEngine.Domain/RedactEngine.Domain.csproj", "RedactEngine.Domain/"]
COPY ["RedactEngine.Infrastructure/RedactEngine.Infrastructure.csproj", "RedactEngine.Infrastructure/"]
COPY ["RedactEngine.Shared/RedactEngine.Shared.csproj", "RedactEngine.Shared/"]
COPY ["RedactEngine.ServiceDefaults/RedactEngine.ServiceDefaults.csproj", "RedactEngine.ServiceDefaults/"]

# 2. Restore dependencies
RUN dotnet restore "./RedactEngine.ApiService/RedactEngine.ApiService.csproj"

# 3. Copy the rest of the source code
COPY . .

# 4. Build
WORKDIR "/src/RedactEngine.ApiService"
RUN dotnet build "./RedactEngine.ApiService.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Stage 3: Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./RedactEngine.ApiService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Stage 4: Final
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RedactEngine.ApiService.dll"]