FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY nuget.config ./
COPY src/Shared/TruckDelivery.Shared.Common/TruckDelivery.Shared.Common.csproj src/Shared/TruckDelivery.Shared.Common/
COPY src/Shared/TruckDelivery.Shared.Contracts/TruckDelivery.Shared.Contracts.csproj src/Shared/TruckDelivery.Shared.Contracts/
COPY src/Shared/TruckDelivery.Shared.Infrastructure/TruckDelivery.Shared.Infrastructure.csproj src/Shared/TruckDelivery.Shared.Infrastructure/
COPY src/Services/Identity/TruckDelivery.Identity.Domain/TruckDelivery.Identity.Domain.csproj src/Services/Identity/TruckDelivery.Identity.Domain/
COPY src/Services/Identity/TruckDelivery.Identity.Application/TruckDelivery.Identity.Application.csproj src/Services/Identity/TruckDelivery.Identity.Application/
COPY src/Services/Identity/TruckDelivery.Identity.Infrastructure/TruckDelivery.Identity.Infrastructure.csproj src/Services/Identity/TruckDelivery.Identity.Infrastructure/
COPY src/Services/Identity/TruckDelivery.Identity.Api/TruckDelivery.Identity.Api.csproj src/Services/Identity/TruckDelivery.Identity.Api/

RUN dotnet restore src/Services/Identity/TruckDelivery.Identity.Api/TruckDelivery.Identity.Api.csproj

COPY src/Shared/ src/Shared/
COPY src/Services/Identity/ src/Services/Identity/

RUN dotnet publish src/Services/Identity/TruckDelivery.Identity.Api/TruckDelivery.Identity.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "TruckDelivery.Identity.Api.dll"]
