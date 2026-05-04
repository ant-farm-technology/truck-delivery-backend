FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Shared/TruckDelivery.Shared.Common/TruckDelivery.Shared.Common.csproj src/Shared/TruckDelivery.Shared.Common/
COPY src/Shared/TruckDelivery.Shared.Contracts/TruckDelivery.Shared.Contracts.csproj src/Shared/TruckDelivery.Shared.Contracts/
COPY src/Shared/TruckDelivery.Shared.Infrastructure/TruckDelivery.Shared.Infrastructure.csproj src/Shared/TruckDelivery.Shared.Infrastructure/
COPY src/Services/Order/TruckDelivery.Order.Domain/TruckDelivery.Order.Domain.csproj src/Services/Order/TruckDelivery.Order.Domain/
COPY src/Services/Order/TruckDelivery.Order.Application/TruckDelivery.Order.Application.csproj src/Services/Order/TruckDelivery.Order.Application/
COPY src/Services/Order/TruckDelivery.Order.Infrastructure/TruckDelivery.Order.Infrastructure.csproj src/Services/Order/TruckDelivery.Order.Infrastructure/
COPY src/Services/Order/TruckDelivery.Order.Api/TruckDelivery.Order.Api.csproj src/Services/Order/TruckDelivery.Order.Api/

RUN dotnet restore src/Services/Order/TruckDelivery.Order.Api/TruckDelivery.Order.Api.csproj

COPY src/Shared/ src/Shared/
COPY src/Services/Order/ src/Services/Order/

RUN dotnet publish src/Services/Order/TruckDelivery.Order.Api/TruckDelivery.Order.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

USER app

COPY --from=build --chown=app:app /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "TruckDelivery.Order.Api.dll"]
