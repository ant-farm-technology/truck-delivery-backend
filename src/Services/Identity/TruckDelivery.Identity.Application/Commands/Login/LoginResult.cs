namespace TruckDelivery.Identity.Application.Commands.Login;

public sealed record LoginResult(string AccessToken, string RefreshToken, DateTime ExpiresAt);
