# Transgrid Mock Server Dockerfile
# Uses .NET 9 SDK for build and runtime (production-ready)
# Note: Update to .NET 10 when it reaches GA

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["Transgrid.MockServer.csproj", "./"]
RUN dotnet restore "Transgrid.MockServer.csproj"

# Copy source code and build
COPY . .
RUN dotnet build "Transgrid.MockServer.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Transgrid.MockServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Copy published files
COPY --from=publish /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl --fail http://localhost:8080/swagger/v1/swagger.json || exit 1

# Run the application
ENTRYPOINT ["dotnet", "Transgrid.MockServer.dll"]
