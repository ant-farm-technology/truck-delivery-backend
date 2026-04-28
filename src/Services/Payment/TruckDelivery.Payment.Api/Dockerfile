FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Shared/TruckDelivery.Shared.Common/TruckDelivery.Shared.Common.csproj src/Shared/TruckDelivery.Shared.Common/
COPY src/Shared/TruckDelivery.Shared.Contracts/TruckDelivery.Shared.Contracts.csproj src/Shared/TruckDelivery.Shared.Contracts/
COPY src/Shared/TruckDelivery.Shared.Infrastructure/TruckDelivery.Shared.Infrastructure.csproj src/Shared/TruckDelivery.Shared.Infrastructure/
COPY src/Services/Payment/TruckDelivery.Payment.Domain/TruckDelivery.Payment.Domain.csproj src/Services/Payment/TruckDelivery.Payment.Domain/
COPY src/Services/Payment/TruckDelivery.Payment.Application/TruckDelivery.Payment.Application.csproj src/Services/Payment/TruckDelivery.Payment.Application/
COPY src/Services/Payment/TruckDelivery.Payment.Infrastructure/TruckDelivery.Payment.Infrastructure.csproj src/Services/Payment/TruckDelivery.Payment.Infrastructure/
COPY src/Services/Payment/TruckDelivery.Payment.Api/TruckDelivery.Payment.Api.csproj src/Services/Payment/TruckDelivery.Payment.Api/

RUN dotnet restore src/Services/Payment/TruckDelivery.Payment.Api/TruckDelivery.Payment.Api.csproj

COPY src/Shared/ src/Shared/
COPY src/Services/Payment/ src/Services/Payment/

RUN dotnet publish src/Services/Payment/TruckDelivery.Payment.Api/TruckDelivery.Payment.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8089
EXPOSE 8089

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -qO- http://localhost:8089/health || exit 1

ENTRYPOINT ["dotnet", "TruckDelivery.Payment.Api.dll"]
