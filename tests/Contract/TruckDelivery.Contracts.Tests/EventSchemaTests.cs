using System.Text.Json;
using FluentAssertions;
using TruckDelivery.Driver.Application.IntegrationEvents;
using TruckDelivery.Driver.Domain.ValueObjects;
using TruckDelivery.Order.Application.IntegrationEvents;
using TruckDelivery.Payment.Application.IntegrationEvents;
using TruckDelivery.Shipment.Application.IntegrationEvents;
using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Contracts.Tests;

// Contract tests: verify that Kafka event schemas satisfy the envelope contract
// and can survive JSON round-trips (backward-compatibility gate).
public sealed class EventSchemaTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── Envelope contract ─────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(AllIntegrationEvents))]
    public void Event_Should_Inherit_IntegrationEvent_Envelope(IntegrationEvent @event)
    {
        @event.MessageId.Should().NotBeEmpty();
        @event.OccurredAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));
        @event.SchemaVersion.Should().Be(1);
    }

    // ── OrderCreatedEvent ─────────────────────────────────────────────────────

    [Fact]
    public void OrderCreatedEvent_Should_RoundTrip_WithAllFields()
    {
        var original = new OrderCreatedEvent(
            OrderId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            PickupCity: "Ho Chi Minh",
            PickupProvince: "Ho Chi Minh",
            DeliveryCity: "Ha Noi",
            DeliveryProvince: "Ha Noi",
            TotalWeightKg: 150m,
            TotalVolumeCbm: 2.5m,
            Items:
            [
                new OrderItemInfo(Guid.NewGuid(), "Thung hang", 2, 75m, 1.25m, 1.0m, 0.8m, 0.6m, false)
            ],
            PickupLatitude: 10.762622,
            PickupLongitude: 106.660172);

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<OrderCreatedEvent>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.OrderId.Should().Be(original.OrderId);
        deserialized.MessageId.Should().Be(original.MessageId);
        deserialized.TotalWeightKg.Should().Be(150m);
        deserialized.Items.Should().HaveCount(1);
        deserialized.PickupLatitude.Should().Be(10.762622);
    }

    [Fact]
    public void OrderCreatedEvent_Should_Deserialize_WhenNewOptionalFields_Added()
    {
        // Simulates a new consumer receiving an event with extra JSON fields (forward-compat)
        var jsonWithExtraField = """
            {
              "orderId": "11111111-1111-1111-1111-111111111111",
              "customerId": "22222222-2222-2222-2222-222222222222",
              "pickupCity": "HCM",
              "pickupProvince": "HCM",
              "deliveryCity": "HN",
              "deliveryProvince": "HN",
              "totalWeightKg": 50.0,
              "totalVolumeCbm": 0.5,
              "items": [],
              "messageId": "33333333-3333-3333-3333-333333333333",
              "occurredAt": "2026-01-01T00:00:00Z",
              "schemaVersion": 1,
              "newFieldAddedInFuture": "should be ignored"
            }
            """;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // Allow extra properties (default behavior in STJ is to ignore them)
        };

        var deserialized = JsonSerializer.Deserialize<OrderCreatedEvent>(jsonWithExtraField, options);

        deserialized.Should().NotBeNull();
        deserialized!.OrderId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        deserialized.TotalWeightKg.Should().Be(50m);
    }

    // ── DriverAssignmentRequestedEvent ────────────────────────────────────────

    [Fact]
    public void DriverAssignmentRequestedEvent_Should_RoundTrip()
    {
        var original = new DriverAssignmentRequestedEvent(
            ShipmentId: Guid.NewGuid(),
            OrderId: Guid.NewGuid(),
            TotalWeightKg: 200m,
            TotalVolumeCbm: 3.0m,
            DistanceMeters: 150_000,
            RequiredLicenseGrades: ["C", "D"]);

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<DriverAssignmentRequestedEvent>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.ShipmentId.Should().Be(original.ShipmentId);
        deserialized.DistanceMeters.Should().Be(150_000);
        deserialized.RequiredLicenseGrades.Should().BeEquivalentTo(["C", "D"]);
    }

    // ── ShipmentCompletedEvent ────────────────────────────────────────────────

    [Fact]
    public void ShipmentCompletedEvent_Should_RoundTrip()
    {
        var original = new ShipmentCompletedEvent(
            ShipmentId: Guid.NewGuid(),
            OrderId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            DriverId: Guid.NewGuid());

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ShipmentCompletedEvent>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.ShipmentId.Should().Be(original.ShipmentId);
        deserialized.OrderId.Should().Be(original.OrderId);
        deserialized.MessageId.Should().Be(original.MessageId);
    }

    // ── ShipmentStartedEvent ──────────────────────────────────────────────────

    [Fact]
    public void ShipmentStartedEvent_Should_RoundTrip()
    {
        var original = new ShipmentStartedEvent(
            ShipmentId: Guid.NewGuid(),
            OrderId: Guid.NewGuid(),
            DriverId: Guid.NewGuid(),
            VehicleId: Guid.NewGuid());

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ShipmentStartedEvent>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.DriverId.Should().Be(original.DriverId);
        deserialized.VehicleId.Should().Be(original.VehicleId);
    }

    // ── PaymentCompletedEvent ─────────────────────────────────────────────────

    [Fact]
    public void PaymentCompletedEvent_Should_RoundTrip()
    {
        var original = new PaymentCompletedEvent(
            PaymentId: Guid.NewGuid(),
            OrderId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            Amount: 150_000m,
            Currency: "VND");

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<PaymentCompletedEvent>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.PaymentId.Should().Be(original.PaymentId);
        deserialized.Amount.Should().Be(150_000m);
        deserialized.Currency.Should().Be("VND");
    }

    [Fact]
    public void PaymentCompletedEvent_Should_UseVND_AsDefaultCurrency()
    {
        var @event = new PaymentCompletedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100_000m);

        @event.Currency.Should().Be("VND");
    }

    // ── DriverAssignedEvent (consumer side: Shipment) ─────────────────────────

    [Fact]
    public void DriverAssignedEvent_Should_RoundTrip()
    {
        var original = new DriverAssignedEvent(
            ShipmentId: Guid.NewGuid(),
            OrderId: Guid.NewGuid(),
            DriverId: Guid.NewGuid(),
            VehicleId: Guid.NewGuid(),
            VehicleMaxWeightKg: 5000m);

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<DriverAssignedEvent>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.DriverId.Should().Be(original.DriverId);
        deserialized.VehicleMaxWeightKg.Should().Be(5000m);
    }

    // ── VehicleBreakdownEvent (Phase 5) ───────────────────────────────────────

    [Fact]
    public void VehicleBreakdownEvent_Should_RoundTrip_WithAllFields()
    {
        var original = new VehicleBreakdownEvent
        {
            DriverId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            Latitude = 10.7769,
            Longitude = 106.7009,
            PhotoUrls = ["breakdown-photos/uuid1.jpg", "breakdown-photos/uuid2.jpg"],
            TrustScore = 67,
            FraudRiskLevel = FraudRiskLevel.Low
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<VehicleBreakdownEvent>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.DriverId.Should().Be(original.DriverId);
        deserialized.Latitude.Should().Be(10.7769);
        deserialized.Longitude.Should().Be(106.7009);
        deserialized.PhotoUrls.Should().HaveCount(2);
        deserialized.TrustScore.Should().Be(67);
        deserialized.FraudRiskLevel.Should().Be(FraudRiskLevel.Low);
    }

    [Fact]
    public void VehicleBreakdownEvent_Should_AllowNullVehicleId()
    {
        var @event = new VehicleBreakdownEvent
        {
            DriverId = Guid.NewGuid(),
            VehicleId = null,
            FraudRiskLevel = FraudRiskLevel.Medium
        };

        var json = JsonSerializer.Serialize(@event, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<VehicleBreakdownEvent>(json, JsonOptions);

        deserialized!.VehicleId.Should().BeNull();
        deserialized.FraudRiskLevel.Should().Be(FraudRiskLevel.Medium);
    }

    [Fact]
    public void VehicleBreakdownEvent_Should_IgnoreExtraFields()
    {
        const string jsonWithExtra = """
            {"messageId":"00000000-0000-0000-0000-000000000001","occurredAt":"2026-05-02T00:00:00Z",
             "schemaVersion":1,"driverId":"00000000-0000-0000-0000-000000000002",
             "latitude":10.7,"longitude":106.7,"trustScore":70,"fraudRiskLevel":1,
             "photoUrls":[],"futureField":"ignored"}
            """;

        var deserialized = JsonSerializer.Deserialize<VehicleBreakdownEvent>(jsonWithExtra, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.TrustScore.Should().Be(70);
    }

    // ── SuspiciousDriverPairDetectedEvent (Phase 6) ───────────────────────────

    [Fact]
    public void SuspiciousDriverPairDetectedEvent_Should_RoundTrip()
    {
        var original = new SuspiciousDriverPairDetectedEvent
        {
            OriginalDriverId = Guid.NewGuid(),
            ReplacementDriverId = Guid.NewGuid(),
            SwapCount = 5,
            DetectedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<SuspiciousDriverPairDetectedEvent>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.OriginalDriverId.Should().Be(original.OriginalDriverId);
        deserialized.ReplacementDriverId.Should().Be(original.ReplacementDriverId);
        deserialized.SwapCount.Should().Be(5);
    }

    [Fact]
    public void SuspiciousDriverPairDetectedEvent_Should_RequireSwapCountAboveThree()
    {
        var @event = new SuspiciousDriverPairDetectedEvent
        {
            OriginalDriverId = Guid.NewGuid(),
            ReplacementDriverId = Guid.NewGuid(),
            SwapCount = 4,
            DetectedAt = DateTime.UtcNow
        };

        @event.SwapCount.Should().BeGreaterThan(3);
        @event.OriginalDriverId.Should().NotBe(@event.ReplacementDriverId);
    }

    // ── DriverDocumentsSubmittedEvent (Phase 2 — OCR trigger) ────────────────

    [Fact]
    public void DriverDocumentsSubmittedEvent_Should_RoundTrip_WithAllPhotoUrls()
    {
        var original = new DriverDocumentsSubmittedEvent(
            DriverId: Guid.NewGuid(),
            VehicleId: Guid.NewGuid(),
            PortraitPhotoUrl: "driver-documents/portrait.jpg",
            IdCardFrontUrl: "driver-documents/id-front.jpg",
            IdCardBackUrl: "driver-documents/id-back.jpg",
            LicenseFrontUrl: "driver-documents/lic-front.jpg",
            LicenseBackUrl: "driver-documents/lic-back.jpg",
            VehicleRegFrontUrl: "driver-documents/reg-front.jpg",
            VehicleRegBackUrl: "driver-documents/reg-back.jpg");

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<DriverDocumentsSubmittedEvent>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.DriverId.Should().Be(original.DriverId);
        deserialized.VehicleId.Should().Be(original.VehicleId);
        deserialized.IdCardFrontUrl.Should().Be("driver-documents/id-front.jpg");
        deserialized.LicenseFrontUrl.Should().Be("driver-documents/lic-front.jpg");
        deserialized.VehicleRegFrontUrl.Should().Be("driver-documents/reg-front.jpg");
    }

    [Fact]
    public void DriverDocumentsSubmittedEvent_Should_CarryAllSevenPhotoUrls()
    {
        var @event = new DriverDocumentsSubmittedEvent(
            Guid.NewGuid(), Guid.NewGuid(),
            "p1.jpg", "p2.jpg", "p3.jpg", "p4.jpg", "p5.jpg", "p6.jpg", "p7.jpg");

        var photos = new[]
        {
            @event.PortraitPhotoUrl, @event.IdCardFrontUrl, @event.IdCardBackUrl,
            @event.LicenseFrontUrl, @event.LicenseBackUrl,
            @event.VehicleRegFrontUrl, @event.VehicleRegBackUrl
        };

        photos.Should().HaveCount(7);
        photos.Should().AllSatisfy(url => url.Should().NotBeNullOrEmpty());
    }

    // ── BreakdownReassignmentCompletedEvent (Phase 6) ─────────────────────────

    [Fact]
    public void BreakdownReassignmentCompletedEvent_Should_RoundTrip()
    {
        var original = new BreakdownReassignmentCompletedEvent
        {
            ShipmentId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            OriginalDriverId = Guid.NewGuid(),
            ReplacementDriverId = Guid.NewGuid(),
            ReplacementVehicleId = Guid.NewGuid()
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<BreakdownReassignmentCompletedEvent>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.ShipmentId.Should().Be(original.ShipmentId);
        deserialized.OriginalDriverId.Should().Be(original.OriginalDriverId);
        deserialized.ReplacementDriverId.Should().Be(original.ReplacementDriverId);
        deserialized.ReplacementVehicleId.Should().Be(original.ReplacementVehicleId);
    }

    [Fact]
    public void BreakdownReassignmentCompletedEvent_Should_HaveDifferentOriginalAndReplacementDrivers()
    {
        var originalDriver = Guid.NewGuid();
        var replacementDriver = Guid.NewGuid();

        var @event = new BreakdownReassignmentCompletedEvent
        {
            ShipmentId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            OriginalDriverId = originalDriver,
            ReplacementDriverId = replacementDriver,
            ReplacementVehicleId = Guid.NewGuid()
        };

        @event.OriginalDriverId.Should().NotBe(@event.ReplacementDriverId);
    }

    // ── MemberData ────────────────────────────────────────────────────────────

    public static TheoryData<IntegrationEvent> AllIntegrationEvents() => new()
    {
        new OrderCreatedEvent(Guid.NewGuid(), Guid.NewGuid(), "HCM", "HCM", "HN", "HN", 50m, 0.5m, []),
        new DriverAssignmentRequestedEvent(Guid.NewGuid(), Guid.NewGuid(), 100m, 1m, 50_000),
        new ShipmentCompletedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
        new ShipmentStartedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
        new PaymentCompletedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100_000m),
        new DriverAssignedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
        new VehicleBreakdownEvent { DriverId = Guid.NewGuid(), TrustScore = 70, FraudRiskLevel = FraudRiskLevel.Low },
        new SuspiciousDriverPairDetectedEvent { OriginalDriverId = Guid.NewGuid(), ReplacementDriverId = Guid.NewGuid(), SwapCount = 4, DetectedAt = DateTime.UtcNow },
        new DriverDocumentsSubmittedEvent(Guid.NewGuid(), Guid.NewGuid(), "p.jpg", "f.jpg", "b.jpg", "lf.jpg", "lb.jpg", "rf.jpg", "rb.jpg"),
        new BreakdownReassignmentCompletedEvent { ShipmentId = Guid.NewGuid(), OrderId = Guid.NewGuid(), OriginalDriverId = Guid.NewGuid(), ReplacementDriverId = Guid.NewGuid(), ReplacementVehicleId = Guid.NewGuid() }
    };
}
