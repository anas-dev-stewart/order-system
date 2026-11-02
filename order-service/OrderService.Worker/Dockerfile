# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Trust the corporate TLS-inspection root CAs so `dotnet restore` can reach
# nuget.org from inside the container's own (otherwise minimal) trust store.
COPY docker-ca-certs/*.crt /usr/local/share/ca-certificates/
RUN update-ca-certificates

COPY order-service/OrderSystem.Contracts/OrderSystem.Contracts.csproj order-service/OrderSystem.Contracts/
COPY order-service/OrderService.Worker/OrderService.Worker.csproj order-service/OrderService.Worker/
RUN dotnet restore order-service/OrderService.Worker/OrderService.Worker.csproj

COPY order-service/OrderSystem.Contracts/ order-service/OrderSystem.Contracts/
COPY order-service/OrderService.Worker/ order-service/OrderService.Worker/
RUN dotnet publish order-service/OrderService.Worker/OrderService.Worker.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# SQLite inbox file lives here; mounted as a named volume in docker-compose
# so processed-message history survives a container restart.
RUN mkdir -p /data
ENV ConnectionStrings__WorkerDb="Data Source=/data/worker-inbox.db"

ENTRYPOINT ["dotnet", "OrderService.Worker.dll"]
