using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Driver.Application.IntegrationEvents;

// Published when driver completes Step 3 of onboarding — triggers OCR async verification
public sealed record DriverDocumentsSubmittedEvent(
    Guid DriverId,
    Guid VehicleId,
    string PortraitPhotoUrl,
    string IdCardFrontUrl,
    string IdCardBackUrl,
    string LicenseFrontUrl,
    string LicenseBackUrl,
    string VehicleRegFrontUrl,
    string VehicleRegBackUrl) : IntegrationEvent;
