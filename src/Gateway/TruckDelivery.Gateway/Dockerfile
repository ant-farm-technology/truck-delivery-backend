FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY nuget.config ./
COPY src/Gateway/TruckDelivery.Gateway/TruckDelivery.Gateway.csproj src/Gateway/TruckDelivery.Gateway/

RUN dotnet restore src/Gateway/TruckDelivery.Gateway/TruckDelivery.Gateway.csproj

COPY src/Gateway/ src/Gateway/

RUN dotnet publish src/Gateway/TruckDelivery.Gateway/TruckDelivery.Gateway.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "TruckDelivery.Gateway.dll"]
