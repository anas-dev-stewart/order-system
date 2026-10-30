# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Trust the corporate TLS-inspection root CAs so `dotnet restore` can reach
# nuget.org from inside the container's own (otherwise minimal) trust store.
COPY docker-ca-certs/*.crt /usr/local/share/ca-certificates/
RUN update-ca-certificates

COPY product-service/ProductService.Api/ProductService.Api.csproj ./
RUN dotnet restore

COPY product-service/ProductService.Api/ .
RUN dotnet publish ProductService.Api.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ProductService.Api.dll"]
