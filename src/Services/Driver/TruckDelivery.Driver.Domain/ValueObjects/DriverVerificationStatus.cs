namespace TruckDelivery.Driver.Domain.ValueObjects;

public enum DriverVerificationStatus
{
    Draft                    = 0,
    PendingOcrVerification   = 1,
    OcrVerified              = 2,
    ManualReview             = 3,
    AdminVerified            = 4,
    Rejected                 = 5
}
