FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Shared/TruckDelivery.Shared.Common/TruckDelivery.Shared.Common.csproj src/Shared/TruckDelivery.Shared.Common/
COPY src/Shared/TruckDelivery.Shared.Contracts/TruckDelivery.Shared.Contracts.csproj src/Shared/TruckDelivery.Shared.Contracts/
COPY src/Shared/TruckDelivery.Shared.Infrastructure/TruckDelivery.Shared.Infrastructure.csproj src/Shared/TruckDelivery.Shared.Infrastructure/
COPY src/Services/Driver/TruckDelivery.Driver.Domain/TruckDelivery.Driver.Domain.csproj src/Services/Driver/TruckDelivery.Driver.Domain/
COPY src/Services/Driver/TruckDelivery.Driver.Application/TruckDelivery.Driver.Application.csproj src/Services/Driver/TruckDelivery.Driver.Application/
COPY src/Services/Driver/TruckDelivery.Driver.Infrastructure/TruckDelivery.Driver.Infrastructure.csproj src/Services/Driver/TruckDelivery.Driver.Infrastructure/
COPY src/Services/Driver/TruckDelivery.Driver.Api/TruckDelivery.Driver.Api.csproj src/Services/Driver/TruckDelivery.Driver.Api/

RUN dotnet restore src/Services/Driver/TruckDelivery.Driver.Api/TruckDelivery.Driver.Api.csproj

COPY src/Shared/ src/Shared/
COPY src/Services/Driver/ src/Services/Driver/

RUN dotnet publish src/Services/Driver/TruckDelivery.Driver.Api/TruckDelivery.Driver.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "TruckDelivery.Driver.Api.dll"]
