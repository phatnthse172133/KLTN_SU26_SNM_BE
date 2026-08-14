using DomainLayer.Entities;
using DomainLayer.InterfaceCore.Email;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InfrastructureLayer.Workers;

public class EmailOutboxWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmailOutboxWorker> _logger;

    public EmailOutboxWorker(IServiceProvider serviceProvider, ILogger<EmailOutboxWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOutboxAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while processing the email outbox. The worker will retry on the next iteration.");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("EmailOutboxWorker stopped because the host is shutting down.");
        }
    }

    protected virtual TimeSpan Interval => TimeSpan.FromSeconds(30);

    protected virtual async Task ProcessOutboxAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var now = DateTime.UtcNow;
        var timeout = now.AddMinutes(-5);

        // Atomic claim using FOR UPDATE SKIP LOCKED for PostgreSQL
        var sql = @"
            UPDATE ""EmailOutbox""
            SET ""Status"" = 'Processing', ""UpdatedAt"" = @now
            WHERE ""Id"" IN (
                SELECT ""Id"" FROM ""EmailOutbox""
                WHERE (""Status"" = 'Pending' OR ""Status"" = 'Failed' OR (""Status"" = 'Processing' AND ""UpdatedAt"" < @timeout))
                  AND (""NextRetryAt"" IS NULL OR ""NextRetryAt"" <= @now)
                  AND ""RetryCount"" < 5
                ORDER BY ""CreatedAt""
                FOR UPDATE SKIP LOCKED
                LIMIT 20
            )
            RETURNING *;";

        var emailsToProcess = await dbContext.EmailOutboxes
            .FromSqlRaw(sql, new Npgsql.NpgsqlParameter("now", now), new Npgsql.NpgsqlParameter("timeout", timeout))
            .ToListAsync(stoppingToken);

        if (!emailsToProcess.Any())
            return;

        foreach (var email in emailsToProcess)
        {
            try
            {
                await emailService.SendHtmlEmailAsync(email.RecipientEmail, email.Subject, email.HtmlBody, stoppingToken);

                email.Status = "Sent";
                email.SentAt = DateTime.UtcNow;
                email.UpdatedAt = DateTime.UtcNow;
                if (string.Equals(email.EmailType, "BoothOwnerInvitation", StringComparison.Ordinal)
                    || string.Equals(email.EmailType, "MarketOwnerInvitation", StringComparison.Ordinal))
                {
                    // Invitation bodies contain a one-time temporary password while queued.
                    // Remove that sensitive content as soon as delivery succeeds.
                    email.HtmlBody = "Account invitation delivered. Temporary credentials were removed after delivery.";
                }
                _logger.LogInformation("Successfully sent email (Type: {Type}) to {Email}", email.EmailType, email.RecipientEmail);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                email.LastError = ex.Message;
                email.RetryCount++;
                email.Status = email.RetryCount >= 5 ? "Failed_Permanent" : "Failed";
                email.NextRetryAt = CalculateNextRetry(email.RetryCount);
                email.UpdatedAt = DateTime.UtcNow;
                _logger.LogWarning(ex, "Failed to send email to {Email}. RetryCount: {RetryCount}", email.RecipientEmail, email.RetryCount);
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
    }

    private DateTime CalculateNextRetry(int retryCount)
    {
        return retryCount switch
        {
            1 => DateTime.UtcNow.AddMinutes(1),
            2 => DateTime.UtcNow.AddMinutes(5),
            3 => DateTime.UtcNow.AddMinutes(15),
            4 => DateTime.UtcNow.AddHours(1),
            _ => DateTime.UtcNow.AddHours(6)
        };
    }
}
