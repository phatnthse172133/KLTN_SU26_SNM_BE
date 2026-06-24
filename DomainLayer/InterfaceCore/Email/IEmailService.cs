namespace DomainLayer.InterfaceCore.Email;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string email, string fullName, string verificationToken, CancellationToken cancellationToken = default);
}
