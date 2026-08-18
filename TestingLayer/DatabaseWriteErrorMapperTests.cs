using System.Text.Json;
using ApplicationLayer.Exceptions;
using DomainLayer.Entities;
using DomainLayer.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using InfrastructureLayer.Cores.Database;
using InfrastructureLayer.Data;

namespace TestingLayer;

public sealed class DatabaseWriteErrorMapperTests
{
    [Fact]
    public void UndefinedColumn_IsSchemaError_NotGenericConflict()
    {
        var mapped = DatabaseWriteErrorMapper.Map(
            new DbUpdateException("could not update", Postgres("42703")));

        Assert.Equal(500, mapped.StatusCode);
        Assert.Equal(DatabaseWriteErrorMapper.SchemaCode, mapped.ErrorCode);
        Assert.Equal("undefined_column", Reason(mapped.Details));
        Assert.Equal("42703", SqlState(mapped.Details));
    }

    [Fact]
    public void UndefinedTable_IsSchemaError_NotGenericConflict()
    {
        var mapped = DatabaseWriteErrorMapper.Map(
            new DbUpdateException("could not update", Postgres("42P01")));

        Assert.Equal(500, mapped.StatusCode);
        Assert.Equal(DatabaseWriteErrorMapper.SchemaCode, mapped.ErrorCode);
    }

    [Fact]
    public void UniqueViolation_IsConflict()
    {
        var mapped = DatabaseWriteErrorMapper.Map(
            new DbUpdateException("could not update", Postgres("23505")));

        Assert.Equal(409, mapped.StatusCode);
        Assert.Equal(DatabaseWriteErrorMapper.ConflictCode, mapped.ErrorCode);
        Assert.Equal("unique_violation", Reason(mapped.Details));
    }

    [Fact]
    public void ForeignKeyViolation_IsConflict()
    {
        var mapped = DatabaseWriteErrorMapper.Map(
            new DbUpdateException("could not update", Postgres("23503")));

        Assert.Equal(409, mapped.StatusCode);
        Assert.Equal(DatabaseWriteErrorMapper.ConflictCode, mapped.ErrorCode);
        Assert.Equal("foreign_key_violation", Reason(mapped.Details));
    }

    [Fact]
    public void BareUndefinedColumnPostgresException_IsSchemaError()
    {
        var mapped = DatabaseWriteErrorMapper.Map(Postgres("42703"));
        Assert.Equal(500, mapped.StatusCode);
        Assert.Equal(DatabaseWriteErrorMapper.SchemaCode, mapped.ErrorCode);
    }

    [Fact]
    public void UnknownSqlState_IsWriteError_NotConflict()
    {
        var mapped = DatabaseWriteErrorMapper.Map(
            new DbUpdateException("could not update", Postgres("57014")));

        Assert.Equal(500, mapped.StatusCode);
        Assert.Equal(DatabaseWriteErrorMapper.WriteCode, mapped.ErrorCode);
    }

    [Fact]
    public async Task Concurrency_IncludesEntityTable_WithoutSqlState()
    {
        var name = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<SNMDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        var createdAt = new DateTime(2026, 8, 18, 3, 0, 0, DateTimeKind.Utc);
        await using (var seed = new SNMDbContext(options))
        {
            seed.AssistantConversations.Add(new AssistantConversation
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                CustomerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Status = AssistantConversationStatus.Active,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            });
            await seed.SaveChangesAsync();
        }

        await using var updater = new SNMDbContext(options);
        var conversation = await updater.AssistantConversations.SingleAsync();
        await using (var deleter = new SNMDbContext(options))
        {
            deleter.AssistantConversations.Remove(await deleter.AssistantConversations.SingleAsync());
            await deleter.SaveChangesAsync();
        }

        conversation.UpdatedAt = createdAt.AddSeconds(1);
        var exception = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => updater.SaveChangesAsync());
        var mapped = DatabaseWriteErrorMapper.Map(exception);

        Assert.Equal(409, mapped.StatusCode);
        Assert.Equal(DatabaseWriteErrorMapper.ConflictCode, mapped.ErrorCode);
        Assert.Equal("concurrency", Reason(mapped.Details));
        Assert.Null(SqlState(mapped.Details));
        Assert.Equal("AssistantConversation", Read(mapped.Details, "table"));
    }

    private static PostgresException Postgres(string sqlState)
        => new("database write failed", "ERROR", "ERROR", sqlState);

    private static string? Reason(object details) => Read(details, "reason");

    private static string? SqlState(object details) => Read(details, "sqlState");

    private static string? Read(object details, string name)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(details));
        return document.RootElement.TryGetProperty(name, out var value) ? value.GetString() : null;
    }
}

internal static class AssistantProviderFailure
{
    public static string Reason(AppException exception)
    {
        Assert.NotNull(exception.Details);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(exception.Details));
        Assert.True(document.RootElement.TryGetProperty("reason", out var reason));
        return reason.GetString() ?? string.Empty;
    }

    public static int? HttpStatus(AppException exception)
    {
        if (exception.Details is null)
            return null;
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(exception.Details));
        return document.RootElement.TryGetProperty("httpStatus", out var status) && status.ValueKind == JsonValueKind.Number
            ? status.GetInt32()
            : null;
    }

    public static string? Stage(AppException exception) => Read(exception, "stage");
    public static string? FinishReason(AppException exception) => Read(exception, "finishReason");
    public static string? ParseFailureCategory(AppException exception) => Read(exception, "parseFailureCategory");
    public static int? ConfiguredMaxOutputTokens(AppException exception) => ReadInt(exception, "configuredMaxOutputTokens");
    public static int? OutputTokenCount(AppException exception) => ReadInt(exception, "outputTokenCount");
    public static int? ResponseCharacterCount(AppException exception) => ReadInt(exception, "responseCharacterCount");

    private static string? Read(AppException exception, string name)
    {
        if (exception.Details is null)
            return null;
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(exception.Details));
        return document.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? ReadInt(AppException exception, string name)
    {
        if (exception.Details is null)
            return null;
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(exception.Details));
        return document.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;
    }
}
