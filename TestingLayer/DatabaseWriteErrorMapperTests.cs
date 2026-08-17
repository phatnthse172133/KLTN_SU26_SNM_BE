using System.Text.Json;
using ApplicationLayer.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using InfrastructureLayer.Cores.Database;

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
}
