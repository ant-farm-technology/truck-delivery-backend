FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Shared/TruckDelivery.Shared.Common/TruckDelivery.Shared.Common.csproj src/Shared/TruckDelivery.Shared.Common/
COPY src/Shared/TruckDelivery.Shared.Contracts/TruckDelivery.Shared.Contracts.csproj src/Shared/TruckDelivery.Shared.Contracts/
COPY src/Shared/TruckDelivery.Shared.Infrastructure/TruckDelivery.Shared.Infrastructure.csproj src/Shared/TruckDelivery.Shared.Infrastructure/
COPY src/Services/Shipment/TruckDelivery.Shipment.Domain/TruckDelivery.Shipment.Domain.csproj src/Services/Shipment/TruckDelivery.Shipment.Domain/
COPY src/Services/Shipment/TruckDelivery.Shipment.Application/TruckDelivery.Shipment.Application.csproj src/Services/Shipment/TruckDelivery.Shipment.Application/
COPY src/Services/Shipment/TruckDelivery.Shipment.Infrastructure/TruckDelivery.Shipment.Infrastructure.csproj src/Services/Shipment/TruckDelivery.Shipment.Infrastructure/
COPY src/Services/Shipment/TruckDelivery.Shipment.Api/TruckDelivery.Shipment.Api.csproj src/Services/Shipment/TruckDelivery.Shipment.Api/

RUN dotnet restore src/Services/Shipment/TruckDelivery.Shipment.Api/TruckDelivery.Shipment.Api.csproj

COPY src/Shared/ src/Shared/
COPY src/Services/Shipment/ src/Services/Shipment/

RUN dotnet publish src/Services/Shipment/TruckDelivery.Shipment.Api/TruckDelivery.Shipment.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8086
EXPOSE 8086

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -qO- http://localhost:8086/health || exit 1

ENTRYPOINT ["dotnet", "TruckDelivery.Shipment.Api.dll"]
