using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TruckDelivery.Shipment.Application.IntegrationEvents;
using TruckDelivery.Shipment.Domain.Aggregates;
using TruckDelivery.Shipment.Domain.Repositories;
using TruckDelivery.Shipment.Infrastructure.Persistence.EFCore;
using TruckDelivery.Shipment.Infrastructure.Persistence.Mongo;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Shipment.Infrastructure.Messaging.Kafka.Consumers;

/// <summary>
/// Polls Reassigning shipments and re-enters the dispatch pipeline by
/// transitioning to DriverAssigning and publishing DriverAssignmentRequestedEvent.
/// The existing DriverAssignedConsumer handles the rest when Driver Service responds.
/// </summary>
public sealed class BreakdownSagaOrchestrator(
    IServiceScopeFactory scopeFactory,
    ILogger<BreakdownSagaOrchestrator> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(10);
    private const int MaxRetries = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("BreakdownSagaOrchestrator started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessReassigningShipmentsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "BreakdownSagaOrchestrator batch failed");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessReassigningShipmentsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShipmentDbContext>();
        var breakdownSagaRepo = scope.ServiceProvider.GetRequiredService<IBreakdownSagaRepository>();
        var shipmentRepo = scope.ServiceProvider.GetRequiredService<IShipmentRepository>();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var reassigning = await db.Shipments
            .Where(s => s.Status == ShipmentStatus.Reassigning)
            .Take(10)
            .ToListAsync(ct);

        foreach (var shipment in reassigning)
        {
            var saga = await breakdownSagaRepo.GetByShipmentIdAsync(shipment.Id, ct)
                ?? new BreakdownSagaState
                {
                    SagaId = Guid.NewGuid(),
                    ShipmentId = shipment.Id,
                    OriginalDriverId = Guid.Empty, // driver already cleared from shipment
                    Status = BreakdownSagaStatus.Started,
                    StartedAt = DateTime.UtcNow
                };

            if (saga.RetryCount >= MaxRetries)
            {
                logger.LogWarning("Breakdown saga exceeded max retries for Shipment={ShipmentId} — failing", shipment.Id);
                shipment.Fail("Breakdown: no replacement driver found after maximum retries");
                await shipmentRepo.UpdateAsync(shipment, ct);
                saga.Status = BreakdownSagaStatus.Failed;
                saga.FailedAt = DateTime.UtcNow;
                await breakdownSagaRepo.UpsertAsync(saga, ct);
                await uow.SaveChangesAsync(ct);
                continue;
            }

            try
            {
                await ReenterDispatchPipelineAsync(shipment, saga, shipmentRepo, breakdownSagaRepo, outboxRepo, uow, ct);
            }
            catch (Exception ex)
            {
                saga.RetryCount++;
                saga.FailureReason = ex.Message;
                await breakdownSagaRepo.UpsertAsync(saga, ct);
                logger.LogError(ex, "BreakdownSaga step failed for Shipment={ShipmentId} Retry={Retry}", shipment.Id, saga.RetryCount);
            }
        }
    }

    private async Task ReenterDispatchPipelineAsync(
        Domain.Aggregates.Shipment shipment,
        BreakdownSagaState saga,
        IShipmentRepository shipmentRepo,
        IBreakdownSagaRepository breakdownSagaRepo,
        IOutboxRepository outboxRepo,
        IUnitOfWork uow,
        CancellationToken ct)
    {
        // Transition Reassigning → DriverAssigning so Driver Service can find a new driver
        var result = shipment.TransitionTo(ShipmentStatus.DriverAssigning);
        if (result.IsFailure)
        {
            logger.LogWarning("Cannot transition Shipment={ShipmentId} to DriverAssigning: {Error}",
                shipment.Id, result.Error.Description);
            return;
        }

        var requestEvent = new DriverAssignmentRequestedEvent(
            shipment.Id,
            shipment.OrderId,
            shipment.TotalWeightKg,
            shipment.TotalVolumeCbm,
            shipment.Route?.DistanceMeters ?? 0);

        await outboxRepo.AddAsync(OutboxMessage.Create(
            nameof(DriverAssignmentRequestedEvent),
            "shipment.driver.assignment-requested",
            shipment.Id.ToString(),
            JsonSerializer.Serialize(requestEvent)), ct);

        saga.Status = BreakdownSagaStatus.ReassignmentRequested;
        saga.RetryCount++;

        await shipmentRepo.UpdateAsync(shipment, ct);
        await breakdownSagaRepo.UpsertAsync(saga, ct);
        await uow.SaveChangesAsync(ct);

        logger.LogInformation(
            "Shipment={ShipmentId} re-entered dispatch pipeline for breakdown reassignment (attempt {Retry})",
            shipment.Id, saga.RetryCount);
    }
}
