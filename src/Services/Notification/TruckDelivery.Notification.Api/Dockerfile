FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Shared/TruckDelivery.Shared.Common/TruckDelivery.Shared.Common.csproj src/Shared/TruckDelivery.Shared.Common/
COPY src/Shared/TruckDelivery.Shared.Contracts/TruckDelivery.Shared.Contracts.csproj src/Shared/TruckDelivery.Shared.Contracts/
COPY src/Shared/TruckDelivery.Shared.Infrastructure/TruckDelivery.Shared.Infrastructure.csproj src/Shared/TruckDelivery.Shared.Infrastructure/
COPY src/Services/Notification/TruckDelivery.Notification.Domain/TruckDelivery.Notification.Domain.csproj src/Services/Notification/TruckDelivery.Notification.Domain/
COPY src/Services/Notification/TruckDelivery.Notification.Application/TruckDelivery.Notification.Application.csproj src/Services/Notification/TruckDelivery.Notification.Application/
COPY src/Services/Notification/TruckDelivery.Notification.Infrastructure/TruckDelivery.Notification.Infrastructure.csproj src/Services/Notification/TruckDelivery.Notification.Infrastructure/
COPY src/Services/Notification/TruckDelivery.Notification.Api/TruckDelivery.Notification.Api.csproj src/Services/Notification/TruckDelivery.Notification.Api/

RUN dotnet restore src/Services/Notification/TruckDelivery.Notification.Api/TruckDelivery.Notification.Api.csproj

COPY src/Shared/ src/Shared/
COPY src/Services/Notification/ src/Services/Notification/

RUN dotnet publish src/Services/Notification/TruckDelivery.Notification.Api/TruckDelivery.Notification.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

USER app

COPY --from=build --chown=app:app /app/publish .

ENV ASPNETCORE_URLS=http://+:8088
EXPOSE 8088

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -qO- http://localhost:8088/health || exit 1

ENTRYPOINT ["dotnet", "TruckDelivery.Notification.Api.dll"]
