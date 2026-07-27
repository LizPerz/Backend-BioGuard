namespace BioGuard.Api.Services;

public interface IEmailService
{
    Task<bool> SendVerificationCodeAsync(string toEmail, string nombre, string code);
    Task<bool> SendPasswordResetAsync(string toEmail, string nombre, string resetLink);
}
