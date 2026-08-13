using InfrastructureLayer.Cores.Emails;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace TestingLayer;

public class EmailServiceTests
{
    [Fact]
    public void CanSend_WhenSmtpAndVerificationEndpointMissing_IsFalse()
    {
        var service = CreateService(new Dictionary<string, string?>());

        Assert.False(service.CanSendVerificationEmail());
        Assert.False(service.CanSendPasswordResetEmail());
    }

    [Fact]
    public void CanSendVerificationEmail_RequiresEndpointAndSmtp()
    {
        var smtpOnly = CreateService(new Dictionary<string, string?>
        {
            ["Email:SmtpHost"] = "smtp.example.com",
            ["Email:FromAddress"] = "noreply@example.com",
            ["Email:Port"] = "587"
        });
        var complete = CreateService(new Dictionary<string, string?>
        {
            ["Email:SmtpHost"] = "smtp.example.com",
            ["Email:FromAddress"] = "noreply@example.com",
            ["Email:Port"] = "587",
            ["Auth:VerificationEndpoint"] = "http://localhost:5282/api/auth/verify-email"
        });

        Assert.False(smtpOnly.CanSendVerificationEmail());
        Assert.True(smtpOnly.CanSendPasswordResetEmail());
        Assert.True(complete.CanSendVerificationEmail());
        Assert.True(complete.CanSendPasswordResetEmail());
    }

    [Fact]
    public void CanSend_WhenCredentialsAreMismatched_IsFalse()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["Email:SmtpHost"] = "smtp.example.com",
            ["Email:FromAddress"] = "noreply@example.com",
            ["Email:Username"] = "noreply@example.com",
            ["Email:Password"] = "",
            ["Auth:VerificationEndpoint"] = "http://localhost:5282/api/auth/verify-email"
        });

        Assert.False(service.CanSendVerificationEmail());
        Assert.False(service.CanSendPasswordResetEmail());
    }

    [Fact]
    public async Task SendVerificationEmailAsync_WhenNotConfigured_ThrowsWithoutHanging()
    {
        var service = CreateService(new Dictionary<string, string?>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendVerificationEmailAsync("user@example.com", "User", "token"));

        Assert.Contains("not configured", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendPasswordResetOtpAsync_WhenSmtpMissing_ThrowsWithoutHanging()
    {
        var service = CreateService(new Dictionary<string, string?>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendPasswordResetOtpAsync("user@example.com", "User", "123456"));
    }

    private static EmailService CreateService(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new EmailService(configuration, NullLogger<EmailService>.Instance);
    }
}
