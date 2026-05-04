using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using TruckDelivery.Notification.Application.Interfaces;

namespace TruckDelivery.Notification.Infrastructure.Notifications;

public sealed class SmtpEmailSender : IEmailNotificationSender
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _fromAddress;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _logger = logger;
        _host = configuration["Smtp:Host"]
            ?? throw new InvalidOperationException("Smtp:Host not configured");
        _port = int.TryParse(configuration["Smtp:Port"], out var port) ? port : 587;
        _username = configuration["Smtp:Username"]
            ?? throw new InvalidOperationException("Smtp:Username not configured");
        _password = configuration["Smtp:Password"]
            ?? throw new InvalidOperationException("Smtp:Password not configured");
        _fromAddress = configuration["Smtp:FromAddress"] ?? _username;
    }

    public async Task SendAsync(string email, string subject, string body, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_fromAddress));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = body };

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(_host, _port, SecureSocketOptions.StartTls, ct);
        await smtp.AuthenticateAsync(_username, _password, ct);
        await smtp.SendAsync(message, ct);
        await smtp.DisconnectAsync(quit: true, ct);

        _logger.LogInformation("Email sent via SMTP To={Email} Subject={Subject}", email, subject);
    }
}
