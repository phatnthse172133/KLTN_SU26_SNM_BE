namespace DomainLayer.InterfaceCore.Email;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string email, string fullName, string verificationToken, CancellationToken cancellationToken = default);
    Task SendPasswordResetOtpAsync(string email, string fullName, string otp, CancellationToken cancellationToken = default);
}
