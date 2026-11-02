# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Trust the corporate TLS-inspection root CAs so `dotnet restore` can reach
# nuget.org from inside the container's own (otherwise minimal) trust store.
COPY docker-ca-certs/*.crt /usr/local/share/ca-certificates/
RUN update-ca-certificates

# Project files first (preserving the same relative layout as the repo, so
# OrderService.Api's "..\OrderSystem.Contracts\..." ProjectReference still
# resolves) so `dotnet restore` is cached and only reruns when a .csproj
# actually changes, not on every source edit.
COPY order-service/OrderSystem.Contracts/OrderSystem.Contracts.csproj order-service/OrderSystem.Contracts/
COPY order-service/OrderService.Api/OrderService.Api.csproj order-service/OrderService.Api/
RUN dotnet restore order-service/OrderService.Api/OrderService.Api.csproj

COPY order-service/OrderSystem.Contracts/ order-service/OrderSystem.Contracts/
COPY order-service/OrderService.Api/ order-service/OrderService.Api/
RUN dotnet publish order-service/OrderService.Api/OrderService.Api.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "OrderService.Api.dll"]
