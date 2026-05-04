FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Shared/TruckDelivery.Shared.Common/TruckDelivery.Shared.Common.csproj src/Shared/TruckDelivery.Shared.Common/
COPY src/Shared/TruckDelivery.Shared.Contracts/TruckDelivery.Shared.Contracts.csproj src/Shared/TruckDelivery.Shared.Contracts/
COPY src/Shared/TruckDelivery.Shared.Infrastructure/TruckDelivery.Shared.Infrastructure.csproj src/Shared/TruckDelivery.Shared.Infrastructure/
COPY src/Services/Analytics/TruckDelivery.Analytics.Domain/TruckDelivery.Analytics.Domain.csproj src/Services/Analytics/TruckDelivery.Analytics.Domain/
COPY src/Services/Analytics/TruckDelivery.Analytics.Application/TruckDelivery.Analytics.Application.csproj src/Services/Analytics/TruckDelivery.Analytics.Application/
COPY src/Services/Analytics/TruckDelivery.Analytics.Infrastructure/TruckDelivery.Analytics.Infrastructure.csproj src/Services/Analytics/TruckDelivery.Analytics.Infrastructure/
COPY src/Services/Analytics/TruckDelivery.Analytics.Api/TruckDelivery.Analytics.Api.csproj src/Services/Analytics/TruckDelivery.Analytics.Api/

RUN dotnet restore src/Services/Analytics/TruckDelivery.Analytics.Api/TruckDelivery.Analytics.Api.csproj

COPY src/Shared/ src/Shared/
COPY src/Services/Analytics/ src/Services/Analytics/

RUN dotnet publish src/Services/Analytics/TruckDelivery.Analytics.Api/TruckDelivery.Analytics.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

USER app

COPY --from=build --chown=app:app /app/publish .

ENV ASPNETCORE_URLS=http://+:8095
EXPOSE 8095

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -qO- http://localhost:8095/health || exit 1

ENTRYPOINT ["dotnet", "TruckDelivery.Analytics.Api.dll"]
