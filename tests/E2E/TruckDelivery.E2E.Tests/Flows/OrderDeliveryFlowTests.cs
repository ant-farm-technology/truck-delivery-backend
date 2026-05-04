using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using TruckDelivery.E2E.Tests.Fixtures;
using TruckDelivery.E2E.Tests.Helpers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace TruckDelivery.E2E.Tests.Flows;

/// <summary>
/// End-to-end test: Create Order → Driver Assigned → Deliver → COD Payment Completed.
/// Uses real Kafka events between in-process services and real MySQL/MongoDB databases.
/// Optimizer and Route service are stubbed via WireMock.
/// </summary>
[Collection("E2E")]
public sealed class OrderDeliveryFlowTests(E2ETestFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────────

    private static void SetBearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    /// <summary>
    /// Sets up WireMock stubs so the Shipment Dispatch Saga can:
    /// 1. GET /route → distance/duration
    /// 2. POST /optimize → assigns the given driver+vehicle to the given order
    /// 3. POST /bin-check → all packages fit, no dispatcher confirmation needed
    /// </summary>
    private void StubExternalServices(Guid driverId, Guid vehicleId, Guid orderId)
    {
        fixture.WireMock
            .Given(Request.Create().WithPath("/route").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                    {"distance_meters":50000,"duration_seconds":3600,"encoded_polyline":null}
                    """));

        var optimizerBody = JsonSerializer.Serialize(new
        {
            assignments = new[]
            {
                new
                {
                    driver_id = driverId,
                    vehicle_id = vehicleId,
                    order_ids = new[] { orderId }
                }
            },
            unassigned_order_ids = Array.Empty<Guid>(),
            feasible = true,
            strategy_used = "vrp"
        });

        fixture.WireMock
            .Given(Request.Create().WithPath("/optimize").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(optimizerBody));

        fixture.WireMock
            .Given(Request.Create().WithPath("/bin-check").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                    {
                      "all_fit":true,
                      "requires_tilt":false,
                      "requires_dispatcher_confirmation":false,
                      "accessibility_warnings":[]
                    }
                    """));
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Tests
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FullCodFlow_Should_CompleteOrder_And_AutoCompletePayment()
    {
        // ── Setup IDs and tokens ──────────────────────────────────────────────
        var adminId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var adminToken = JwtHelper.AdminToken(adminId);
        var driverToken = JwtHelper.DriverToken(driverUserId);
        var customerToken = JwtHelper.CustomerToken(customerId);

        // ── 1. Admin registers driver via admin endpoint ──────────────────────
        SetBearer(fixture.DriverClient, adminToken);
        var registerDriverResp = await fixture.DriverClient.PostAsJsonAsync("/api/v1/drivers", new
        {
            UserId = driverUserId,
            FullName = "Tài xế E2E",
            LicenseGrade = "C",
            LicenseExpiryDate = "2030-01-01"
        });
        registerDriverResp.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);

        // ── 2. Admin creates vehicle ──────────────────────────────────────────
        var createVehicleResp = await fixture.DriverClient.PostAsJsonAsync("/api/v1/vehicles", new
        {
            LicensePlate = "51A-E2E-01",
            Brand = "HINO",
            Model = "500",
            Type = 3,             // Truck3T
            MaxWeightKg = 3000.0,
            MaxVolumeCbm = 15.0,
            LengthM = 4.2,
            WidthM = 1.8,
            HeightM = 1.8,
            YearOfManufacture = 2022,
            RegistrationNumber = "HCM-22-E2E",
            RegistrationExpiryDate = "2027-12-31"
        });
        createVehicleResp.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        var vehicleContent = await createVehicleResp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var vehicleId = vehicleContent.TryGetProperty("data", out var vData)
            ? vData.GetProperty("vehicleId").GetGuid()
            : vehicleContent.GetProperty("vehicleId").GetGuid();

        // ── 3. Admin assigns vehicle to driver ────────────────────────────────
        var assignResp = await fixture.DriverClient.PostAsJsonAsync(
            $"/api/v1/drivers/{driverUserId}/assign-vehicle",
            new { VehicleId = vehicleId });
        assignResp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

        // ── 4. Admin verifies driver (skip OCR in E2E) ───────────────────────
        var verifyResp = await fixture.DriverClient.PostAsJsonAsync(
            $"/api/v1/drivers/{driverUserId}/verify", new { });
        verifyResp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

        // ── 5. Driver sets status to Available ────────────────────────────────
        SetBearer(fixture.DriverClient, driverToken);
        var availableResp = await fixture.DriverClient.PutAsJsonAsync(
            $"/api/v1/drivers/{driverUserId}/status", new { Status = "Available" });
        availableResp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

        // ── 6. Customer creates order ──────────────────────────────────────────
        SetBearer(fixture.OrderClient, customerToken);
        var createOrderResp = await fixture.OrderClient.PostAsJsonAsync("/api/v1/orders", new
        {
            PickupAddress = new
            {
                Street = "123 Nguyen Hue",
                City = "Ho Chi Minh",
                Province = "Ho Chi Minh",
                PostalCode = "70000",
                CountryCode = "VN",
                Latitude = 10.7769,
                Longitude = 106.7009
            },
            DeliveryAddress = new
            {
                Street = "456 Le Loi",
                City = "Ha Noi",
                Province = "Ha Noi",
                PostalCode = "10000",
                CountryCode = "VN",
                Latitude = 21.0285,
                Longitude = 105.8542
            },
            Items = new[]
            {
                new
                {
                    ProductName = "Tủ lạnh Samsung",
                    Quantity = 1,
                    WeightKg = 45.0,
                    VolumeCbm = 0.756,
                    LengthM = 0.6,
                    WidthM = 0.7,
                    HeightM = 1.8,
                    CanTilt = false
                }
            }
        });
        createOrderResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var orderContent = await createOrderResp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var orderId = orderContent.TryGetProperty("data", out var oData)
            ? oData.GetProperty("orderId").GetGuid()
            : orderContent.GetProperty("orderId").GetGuid();

        // ── 7. Configure WireMock stubs with actual driver+vehicle+order IDs ──
        StubExternalServices(driverUserId, vehicleId, orderId);

        // ── 8. Wait for Saga to assign driver (Kafka events propagate) ─────────
        var assignedOrder = await WaitForAsync.UntilAsync(
            async () =>
            {
                var resp = await fixture.OrderClient.GetFromJsonAsync<JsonElement>(
                    $"/api/v1/orders/{orderId}", JsonOpts);
                return resp;
            },
            order =>
            {
                var status = order.TryGetProperty("data", out var d)
                    ? d.GetProperty("status").GetString()
                    : order.GetProperty("status").GetString();
                return status is "AssignedToDriver";
            },
            timeout: TimeSpan.FromSeconds(60),
            description: $"Order {orderId} Status=AssignedToDriver");

        // ── 9. Get shipmentId from order ───────────────────────────────────────
        var orderData = assignedOrder.TryGetProperty("data", out var od) ? od : assignedOrder;
        var shipmentId = orderData.GetProperty("shipmentId").GetGuid();
        shipmentId.Should().NotBe(Guid.Empty);

        // ── 10. Driver picks up the package ────────────────────────────────────
        SetBearer(fixture.ShipmentClient, driverToken);
        var pickupResp = await fixture.ShipmentClient.PutAsJsonAsync(
            $"/api/v1/shipments/{shipmentId}/status", new { Status = "PickedUp" });
        pickupResp.IsSuccessStatusCode.Should().BeTrue();

        // ── 11. Driver marks in transit ────────────────────────────────────────
        var transitResp = await fixture.ShipmentClient.PutAsJsonAsync(
            $"/api/v1/shipments/{shipmentId}/status", new { Status = "InTransit" });
        transitResp.IsSuccessStatusCode.Should().BeTrue();

        // ── 12. Driver delivers ────────────────────────────────────────────────
        var deliverResp = await fixture.ShipmentClient.PutAsJsonAsync(
            $"/api/v1/shipments/{shipmentId}/status", new { Status = "Delivered" });
        deliverResp.IsSuccessStatusCode.Should().BeTrue();

        // ── 13. Wait for COD payment to auto-complete ──────────────────────────
        SetBearer(fixture.PaymentClient, customerToken);
        var payment = await WaitForAsync.UntilAsync(
            async () => await fixture.PaymentClient.GetFromJsonAsync<JsonElement>(
                $"/api/v1/payments/orders/{orderId}", JsonOpts),
            p =>
            {
                var status = p.TryGetProperty("data", out var d)
                    ? d.GetProperty("status").GetString()
                    : p.GetProperty("status").GetString();
                return status is "Completed";
            },
            timeout: TimeSpan.FromSeconds(30),
            description: $"Payment for order {orderId} Status=Completed");

        var paymentData = payment.TryGetProperty("data", out var pd) ? pd : payment;
        paymentData.GetProperty("status").GetString().Should().Be("Completed");
        paymentData.GetProperty("method").GetString().Should().Be("Cod");
    }

    [Fact]
    public async Task CancelOrder_Should_Return204_WhenOrderIsPending()
    {
        var customerId = Guid.NewGuid();
        var customerToken = JwtHelper.CustomerToken(customerId);

        SetBearer(fixture.OrderClient, customerToken);
        var createResp = await fixture.OrderClient.PostAsJsonAsync("/api/v1/orders", new
        {
            PickupAddress = new { Street = "A", City = "HCM", Province = "HCM", PostalCode = "70000", CountryCode = "VN" },
            DeliveryAddress = new { Street = "B", City = "HN", Province = "HN", PostalCode = "10000", CountryCode = "VN" },
            Items = new[] { new { ProductName = "Box", Quantity = 1, WeightKg = 5.0, VolumeCbm = 0.1 } }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var orderContent = await createResp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var orderId = orderContent.TryGetProperty("data", out var d)
            ? d.GetProperty("orderId").GetGuid()
            : orderContent.GetProperty("orderId").GetGuid();

        var cancelResp = await fixture.OrderClient.DeleteAsync($"/api/v1/orders/{orderId}");
        cancelResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
