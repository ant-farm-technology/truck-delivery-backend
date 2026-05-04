using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TruckDelivery.Shipment.Application.IntegrationEvents;
using TruckDelivery.Shipment.Domain.Aggregates;
using TruckDelivery.Shipment.Domain.Repositories;
using TruckDelivery.Shipment.Domain.ValueObjects;
using TruckDelivery.Shipment.Infrastructure.HttpClients;
using TruckDelivery.Shipment.Infrastructure.Persistence.EFCore;
using TruckDelivery.Shipment.Infrastructure.Persistence.Mongo;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Shipment.Infrastructure.Messaging.Kafka.Consumers;

/// <summary>
/// Polls newly-created shipments and drives the dispatch saga:
/// Created → RoutePlanning → DriverAssigning → (Driver Service takes over)
/// </summary>
public sealed class DispatchSagaOrchestrator(
    IServiceScopeFactory scopeFactory,
    ILogger<DispatchSagaOrchestrator> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private const int MaxRetries = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DispatchSagaOrchestrator started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingShipmentsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "DispatchSagaOrchestrator batch failed");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingShipmentsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShipmentDbContext>();
        var sagaRepo = scope.ServiceProvider.GetRequiredService<ISagaRepository>();
        var shipmentRepo = scope.ServiceProvider.GetRequiredService<IShipmentRepository>();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var routeClient = scope.ServiceProvider.GetRequiredService<RouteServiceClient>();
        var optimizerClient = scope.ServiceProvider.GetRequiredService<OptimizerServiceClient>();

        // Pick up shipments stuck in Created or RoutePlanning
        var pending = await db.Shipments
            .Where(s => s.Status == ShipmentStatus.Created || s.Status == ShipmentStatus.RoutePlanning)
            .Take(20)
            .ToListAsync(ct);

        foreach (var shipment in pending)
        {
            var saga = await sagaRepo.GetByShipmentIdAsync(shipment.Id, ct)
                ?? new ShipmentSagaState
                {
                    SagaId = Guid.NewGuid(),
                    ShipmentId = shipment.Id,
                    OrderId = shipment.OrderId,
                    Status = ShipmentSagaStatus.Started,
                    StartedAt = DateTime.UtcNow
                };

            if (saga.RetryCount >= MaxRetries)
            {
                logger.LogWarning("Shipment {ShipmentId} exceeded max saga retries — failing", shipment.Id);
                shipment.Fail("Exceeded maximum dispatch retries");
                var failedEvent = new ShipmentFailedEvent(shipment.Id, shipment.OrderId, "Exceeded maximum dispatch retries");
                await outboxRepo.AddAsync(OutboxMessage.Create(
                    nameof(ShipmentFailedEvent),
                    "shipment.shipment.failed",
                    shipment.Id.ToString(),
                    JsonSerializer.Serialize(failedEvent)), ct);
                await shipmentRepo.UpdateAsync(shipment, ct);
                saga.Status = ShipmentSagaStatus.Failed;
                saga.FailedAt = DateTime.UtcNow;
                await sagaRepo.UpsertAsync(saga, ct);
                await uow.SaveChangesAsync(ct);
                continue;
            }

            try
            {
                await RunSagaStepAsync(shipment, saga, sagaRepo, shipmentRepo, outboxRepo, uow, routeClient, optimizerClient, ct);
            }
            catch (Exception ex)
            {
                saga.RetryCount++;
                saga.FailureReason = ex.Message;
                await sagaRepo.UpsertAsync(saga, ct);
                logger.LogError(ex, "Saga step failed for ShipmentId={ShipmentId} Retry={Retry}", shipment.Id, saga.RetryCount);
            }
        }
    }

    private static double ComputeHaversineMeters(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6_371_000; // Earth radius in metres
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLng = (lng2 - lng1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
              * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private async Task RunSagaStepAsync(
        Domain.Aggregates.Shipment shipment,
        ShipmentSagaState saga,
        ISagaRepository sagaRepo,
        IShipmentRepository shipmentRepo,
        IOutboxRepository outboxRepo,
        IUnitOfWork uow,
        RouteServiceClient routeClient,
        OptimizerServiceClient optimizerClient,
        CancellationToken ct)
    {
        // Step 1: route planning — call Route Service if coordinates are available
        if (shipment.Status == ShipmentStatus.Created)
        {
            shipment.TransitionTo(ShipmentStatus.RoutePlanning);

            RouteInfo routeInfo;
            if (shipment.PickupLatitude.HasValue && shipment.PickupLongitude.HasValue
                && shipment.DeliveryLatitude.HasValue && shipment.DeliveryLongitude.HasValue)
            {
                var routeResult = await routeClient.GetRouteAsync(
                    shipment.PickupLatitude.Value, shipment.PickupLongitude.Value,
                    shipment.DeliveryLatitude.Value, shipment.DeliveryLongitude.Value,
                    ct);

                if (routeResult is not null)
                {
                    routeInfo = RouteInfo.Create(routeResult.DistanceMeters, routeResult.DurationSeconds, routeResult.EncodedPolyline).Value;
                    logger.LogInformation("Route fetched: {Distance}m {Duration}s for Shipment {ShipmentId}",
                        routeResult.DistanceMeters, routeResult.DurationSeconds, shipment.Id);
                }
                else
                {
                    logger.LogWarning("RouteService returned null for Shipment {ShipmentId} — using Haversine fallback", shipment.Id);
                    var haversine = ComputeHaversineMeters(
                        shipment.PickupLatitude.Value, shipment.PickupLongitude.Value,
                        shipment.DeliveryLatitude.Value, shipment.DeliveryLongitude.Value);
                    routeInfo = RouteInfo.Create(haversine, haversine / 15.0).Value; // ~15 m/s avg truck speed
                }
            }
            else
            {
                // No coordinates supplied — use static placeholder until coordinates are available
                logger.LogWarning("No coordinates for Shipment {ShipmentId} — using static route placeholder", shipment.Id);
                routeInfo = RouteInfo.Create(50_000, 3600).Value;
            }

            shipment.SetRoute(routeInfo);
            saga.DistanceMeters = routeInfo.DistanceMeters;
            saga.CompletedSteps.Add("RoutePlanned");
            saga.Status = ShipmentSagaStatus.RoutePlanned;

            await shipmentRepo.UpdateAsync(shipment, ct);
            await sagaRepo.UpsertAsync(saga, ct);
            await uow.SaveChangesAsync(ct);
            return;
        }

        // Step 2: request driver assignment via Kafka
        if (shipment.Status == ShipmentStatus.RoutePlanning)
        {
            shipment.TransitionTo(ShipmentStatus.DriverAssigning);

            var requestEvent = new DriverAssignmentRequestedEvent(
                shipment.Id,
                shipment.OrderId,
                shipment.TotalWeightKg,
                shipment.TotalVolumeCbm,
                saga.DistanceMeters ?? 0);

            await outboxRepo.AddAsync(OutboxMessage.Create(
                nameof(DriverAssignmentRequestedEvent),
                "shipment.driver.assignment-requested",
                shipment.Id.ToString(),
                JsonSerializer.Serialize(requestEvent)), ct);

            saga.CompletedSteps.Add("DriverRequested");
            saga.Status = ShipmentSagaStatus.DriverRequested;

            await shipmentRepo.UpdateAsync(shipment, ct);
            await sagaRepo.UpsertAsync(saga, ct);
            await uow.SaveChangesAsync(ct);
        }
    }
}
