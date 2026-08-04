using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace BioGuard.Api.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<bool> SendVerificationCodeAsync(string toEmail, string nombre, string code)
    {
        var subject = "BioGuard - Verifica tu correo electrónico";
        var body = $"""
            <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;">
                <h2 style="color: #2563eb;">BioGuard - Verificación de correo</h2>
                <p>Hola <strong>{nombre}</strong>,</p>
                <p>Tu código de verificación es:</p>
                <div style="background: #f3f4f6; padding: 20px; text-align: center; border-radius: 8px; margin: 20px 0;">
                    <span style="font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #1f2937;">{code}</span>
                </div>
                <p style="color: #6b7280;">Este código expira en <strong>10 minutos</strong>.</p>
                <p style="color: #6b7280;">Si no creaste esta cuenta, ignora este correo.</p>
                <hr style="border: none; border-top: 1px solid #e5e7eb; margin: 20px 0;">
                <p style="color: #9ca3af; font-size: 12px;">BioGuard - Sistema de monitoreo glucémico</p>
            </div>
            """;
        return await SendEmailAsync(toEmail, subject, body);
    }

    public async Task<bool> SendPasswordResetAsync(string toEmail, string nombre, string resetLink)
    {
        var subject = "BioGuard - Recupera tu contraseña";
        var body = $"""
            <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;">
                <h2 style="color: #2563eb;">BioGuard - Recuperación de contraseña</h2>
                <p>Hola <strong>{nombre}</strong>,</p>
                <p>Recibimos una solicitud para restablecer tu contraseña.</p>
                <p>Haz clic en el siguiente enlace:</p>
                <a href="{resetLink}" style="display: inline-block; background: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; margin: 10px 0;">Restablecer contraseña</a>
                <p style="color: #6b7280;">Este enlace expira en <strong>1 hora</strong>.</p>
                <p style="color: #6b7280;">Si no solicitaste esto, ignora este correo.</p>
                <hr style="border: none; border-top: 1px solid #e5e7eb; margin: 20px 0;">
                <p style="color: #9ca3af; font-size: 12px;">BioGuard - Sistema de monitoreo glucémico</p>
            </div>
            """;
        return await SendEmailAsync(toEmail, subject, body);
    }

    private async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        try
        {
            var apiKey = FallbackIfEmpty(_config["SendGrid:ApiKey"], Environment.GetEnvironmentVariable("SENDGRID_API_KEY"));
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                return await SendViaSendGridAsync(apiKey, toEmail, subject, htmlBody);
            }
            return await SendViaSmtpAsync(toEmail, subject, htmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email");
            return false;
        }
    }

    private async Task<bool> SendViaSendGridAsync(string apiKey, string toEmail, string subject, string htmlBody)
    {
        var fromEmail = FallbackIfEmpty(_config["SendGrid:From"], Environment.GetEnvironmentVariable("FROM_EMAIL"));
        var fromName = FallbackIfEmpty(_config["SendGrid:FromName"], Environment.GetEnvironmentVariable("FROM_NAME"));

        if (string.IsNullOrWhiteSpace(fromEmail))
        {
            _logger.LogWarning("SendGrid From email not configured - email skipped. Subject: {Subject}", subject);
            return false;
        }

        var client = new SendGridClient(apiKey);
        var from = new EmailAddress(fromEmail, string.IsNullOrWhiteSpace(fromName) ? "BioGuard" : fromName);
        var to = new EmailAddress(toEmail);
        var message = MailHelper.CreateSingleEmail(from, to, subject, null, htmlBody);

        var response = await client.SendEmailAsync(message);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Body.ReadAsStringAsync();
            _logger.LogWarning("SendGrid rejected email. Status: {StatusCode} Body: {Body}", response.StatusCode, responseBody);
            return false;
        }

        _logger.LogInformation("Email sent via SendGrid. Subject: {Subject}", subject);
        return true;
    }

    private async Task<bool> SendViaSmtpAsync(string toEmail, string subject, string htmlBody)
    {
        var host = FallbackIfEmpty(_config["Smtp:Host"], Environment.GetEnvironmentVariable("SMTP_HOST"));
        var portStr = FallbackIfEmpty(_config["Smtp:Port"], Environment.GetEnvironmentVariable("SMTP_PORT"));
        var user = FallbackIfEmpty(_config["Smtp:User"], Environment.GetEnvironmentVariable("SMTP_USER"));
        var pass = FallbackIfEmpty(_config["Smtp:Password"], Environment.GetEnvironmentVariable("SMTP_PASSWORD"));
        var from = FallbackIfEmpty(_config["Smtp:From"], Environment.GetEnvironmentVariable("SMTP_FROM"));

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
        {
            _logger.LogWarning("SMTP not configured - email skipped. Subject: {Subject}", subject);
            return false;
        }

        var port = int.TryParse(portStr, out var p) ? p : 587;

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from ?? user));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(user, pass);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        _logger.LogInformation("Email sent via SMTP. Subject: {Subject}", subject);
        return true;
    }

    private static string? FallbackIfEmpty(string? value, string? fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;
}
