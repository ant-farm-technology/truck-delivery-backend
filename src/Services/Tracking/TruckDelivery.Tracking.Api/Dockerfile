FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Shared/TruckDelivery.Shared.Common/TruckDelivery.Shared.Common.csproj src/Shared/TruckDelivery.Shared.Common/
COPY src/Shared/TruckDelivery.Shared.Contracts/TruckDelivery.Shared.Contracts.csproj src/Shared/TruckDelivery.Shared.Contracts/
COPY src/Shared/TruckDelivery.Shared.Infrastructure/TruckDelivery.Shared.Infrastructure.csproj src/Shared/TruckDelivery.Shared.Infrastructure/
COPY src/Services/Tracking/TruckDelivery.Tracking.Domain/TruckDelivery.Tracking.Domain.csproj src/Services/Tracking/TruckDelivery.Tracking.Domain/
COPY src/Services/Tracking/TruckDelivery.Tracking.Application/TruckDelivery.Tracking.Application.csproj src/Services/Tracking/TruckDelivery.Tracking.Application/
COPY src/Services/Tracking/TruckDelivery.Tracking.Infrastructure/TruckDelivery.Tracking.Infrastructure.csproj src/Services/Tracking/TruckDelivery.Tracking.Infrastructure/
COPY src/Services/Tracking/TruckDelivery.Tracking.Api/TruckDelivery.Tracking.Api.csproj src/Services/Tracking/TruckDelivery.Tracking.Api/

RUN dotnet restore src/Services/Tracking/TruckDelivery.Tracking.Api/TruckDelivery.Tracking.Api.csproj

COPY src/Shared/ src/Shared/
COPY src/Services/Tracking/ src/Services/Tracking/

RUN dotnet publish src/Services/Tracking/TruckDelivery.Tracking.Api/TruckDelivery.Tracking.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

USER app

COPY --from=build --chown=app:app /app/publish .

ENV ASPNETCORE_URLS=http://+:8087
EXPOSE 8087

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -qO- http://localhost:8087/health || exit 1

ENTRYPOINT ["dotnet", "TruckDelivery.Tracking.Api.dll"]
