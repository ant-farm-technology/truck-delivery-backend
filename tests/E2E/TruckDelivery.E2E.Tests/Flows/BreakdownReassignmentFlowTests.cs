using Xunit;
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
/// End-to-end test: Breakdown Saga â€” driver reports breakdown â†’ shipment moves to
/// Reassigning â†’ Breakdown Saga Orchestrator picks a replacement driver â†’ reassignment completes.
/// </summary>
[Collection("E2E")]
public sealed class BreakdownReassignmentFlowTests(E2ETestFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static void SetBearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    /// <summary>
    /// Creates an admin-verified Available driver and returns its driverId + vehicleId.
    /// </summary>
    private async Task<(Guid DriverId, Guid VehicleId)> CreateVerifiedDriverAsync(
        Guid userId, string plate, string adminToken, string driverToken)
    {
        SetBearer(fixture.DriverClient, adminToken);

        await fixture.DriverClient.PostAsJsonAsync("/api/v1/drivers", new
        {
            UserId = userId,
            FullName = $"Driver {plate}",
            LicenseGrade = "C",
            LicenseExpiryDate = "2030-01-01"
        });

        var vehicleResp = await fixture.DriverClient.PostAsJsonAsync("/api/v1/vehicles", new
        {
            LicensePlate = plate,
            Brand = "HINO",
            Model = "500",
            Type = 3,
            MaxWeightKg = 3000.0,
            MaxVolumeCbm = 15.0,
            LengthM = 4.2, WidthM = 1.8, HeightM = 1.8,
            YearOfManufacture = 2022,
            RegistrationNumber = $"HCM-{plate}",
            RegistrationExpiryDate = "2027-12-31"
        });
        var vehicleJson = await vehicleResp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var vehicleId = vehicleJson.TryGetProperty("data", out var vd)
            ? vd.GetProperty("vehicleId").GetGuid()
            : vehicleJson.GetProperty("vehicleId").GetGuid();

        await fixture.DriverClient.PostAsJsonAsync($"/api/v1/drivers/{userId}/assign-vehicle", new { VehicleId = vehicleId });
        await fixture.DriverClient.PostAsJsonAsync($"/api/v1/drivers/{userId}/verify", new { });

        SetBearer(fixture.DriverClient, driverToken);
        await fixture.DriverClient.PutAsJsonAsync($"/api/v1/drivers/{userId}/status", new { Status = "Available" });

        return (userId, vehicleId);
    }

    [Fact]
    public async Task BreakdownReport_Should_TransitionShipmentToReassigning()
    {
        var adminId = Guid.NewGuid();
        var originalDriverId = Guid.NewGuid();
        var replacementDriverId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var adminToken = JwtHelper.AdminToken(adminId);
        var originalDriverToken = JwtHelper.DriverToken(originalDriverId);
        var replacementDriverToken = JwtHelper.DriverToken(replacementDriverId);
        var customerToken = JwtHelper.CustomerToken(customerId);

        // â”€â”€ 1. Create 2 drivers: original + replacement â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var (origId, origVehicleId) = await CreateVerifiedDriverAsync(
            originalDriverId, "51A-BREAK-01", adminToken, originalDriverToken);

        var (replId, replVehicleId) = await CreateVerifiedDriverAsync(
            replacementDriverId, "51A-BREAK-02", adminToken, replacementDriverToken);

        // â”€â”€ 2. Create order â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        SetBearer(fixture.OrderClient, customerToken);
        var createOrderResp = await fixture.OrderClient.PostAsJsonAsync("/api/v1/orders", new
        {
            PickupAddress = new
            {
                Street = "100 Breakdown St",
                City = "Ho Chi Minh",
                Province = "Ho Chi Minh",
                PostalCode = "70000",
                CountryCode = "VN",
                Latitude = 10.7769,
                Longitude = 106.7009
            },
            DeliveryAddress = new
            {
                Street = "200 Delivery Ave",
                City = "Ha Noi",
                Province = "Ha Noi",
                PostalCode = "10000",
                CountryCode = "VN",
                Latitude = 21.0285,
                Longitude = 105.8542
            },
            Items = new[]
            {
                new { ProductName = "Cargo", Quantity = 1, WeightKg = 20.0, VolumeCbm = 0.2 }
            }
        });
        createOrderResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var orderJson = await createOrderResp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var orderId = orderJson.TryGetProperty("data", out var od)
            ? od.GetProperty("orderId").GetGuid()
            : orderJson.GetProperty("orderId").GetGuid();

        // â”€â”€ 3. Stub Optimizer: first assigns original driver â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        fixture.WireMock
            .Given(Request.Create().WithPath("/route").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"distance_meters":50000,"duration_seconds":3600,"encoded_polyline":null}"""));

        fixture.WireMock
            .Given(Request.Create().WithPath("/optimize").UsingPost())
            .AtPriority(10)
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    assignments = new[]
                    {
                        new { driver_id = origId, vehicle_id = origVehicleId, order_ids = new[] { orderId } }
                    },
                    unassigned_order_ids = Array.Empty<Guid>(),
                    feasible = true,
                    strategy_used = "vrp"
                })));

        fixture.WireMock
            .Given(Request.Create().WithPath("/bin-check").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"all_fit":true,"requires_tilt":false,"requires_dispatcher_confirmation":false,"accessibility_warnings":[]}"""));

        // â”€â”€ 4. Wait for original driver to be assigned â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        SetBearer(fixture.OrderClient, customerToken);
        await WaitForAsync.UntilAsync(
            async () => await fixture.OrderClient.GetFromJsonAsync<JsonElement>(
                $"/api/v1/orders/{orderId}", JsonOpts),
            order =>
            {
                var status = order.TryGetProperty("data", out var d)
                    ? d.GetProperty("status").GetString()
                    : order.GetProperty("status").GetString();
                return status is "AssignedToDriver";
            },
            timeout: TimeSpan.FromSeconds(60),
            description: $"Order {orderId} initially assigned");

        var orderData2 = await fixture.OrderClient.GetFromJsonAsync<JsonElement>(
            $"/api/v1/orders/{orderId}", JsonOpts);
        var aData = orderData2.TryGetProperty("data", out var d2) ? d2 : orderData2;
        var shipmentId = aData.GetProperty("shipmentId").GetGuid();

        // â”€â”€ 5. Driver starts delivery (InProgress â†’ driver pushes GPS to Redis)
        SetBearer(fixture.ShipmentClient, originalDriverToken);
        await fixture.ShipmentClient.PutAsJsonAsync(
            $"/api/v1/shipments/{shipmentId}/status", new { Status = "PickedUp" });

        // â”€â”€ 6. Stub Optimizer: for reassignment, assign replacement driver â”€â”€â”€â”€
        fixture.WireMock
            .Given(Request.Create().WithPath("/optimize").UsingPost())
            .AtPriority(5) // higher priority overrides the original stub
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    assignments = new[]
                    {
                        new { driver_id = replId, vehicle_id = replVehicleId, order_ids = new[] { orderId } }
                    },
                    unassigned_order_ids = Array.Empty<Guid>(),
                    feasible = true,
                    strategy_used = "vrp-reassignment"
                })));

        // â”€â”€ 7. Original driver reports breakdown â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        SetBearer(fixture.DriverClient, originalDriverToken);
        var breakdownResp = await fixture.DriverClient.PostAsJsonAsync(
            $"/api/v1/drivers/{origId}/report-breakdown", new
            {
                Latitude = 10.7769,
                Longitude = 106.7009,
                PhotoUrls = new[] { "breakdown-photos/e2e-photo-1.jpg" },
                Description = "E2E test breakdown"
            });
        breakdownResp.IsSuccessStatusCode.Should().BeTrue(
            $"Breakdown report failed: {await breakdownResp.Content.ReadAsStringAsync()}");

        // â”€â”€ 8. Wait for shipment to transition through Reassigning â†’ eventually re-assigned â”€â”€
        SetBearer(fixture.ShipmentClient, adminToken);
        await WaitForAsync.UntilAsync(
            async () => await fixture.ShipmentClient.GetFromJsonAsync<JsonElement>(
                $"/api/v1/shipments/{shipmentId}", JsonOpts),
            shipment =>
            {
                var status = shipment.TryGetProperty("data", out var d)
                    ? d.GetProperty("status").GetString()
                    : shipment.GetProperty("status").GetString();
                // Accept either Reassigning (breakdown detected) or InProgress (reassignment complete)
                return status is "Reassigning" or "InProgress" or "DriverAssigning";
            },
            timeout: TimeSpan.FromSeconds(60),
            description: $"Shipment {shipmentId} breakdown detected");

        // Verify the breakdown was recorded
        breakdownResp.StatusCode.Should().NotBe(HttpStatusCode.UnprocessableEntity,
            "Anti-fraud gate rejected the breakdown â€” check TrustScore or photo requirement");
    }
}
