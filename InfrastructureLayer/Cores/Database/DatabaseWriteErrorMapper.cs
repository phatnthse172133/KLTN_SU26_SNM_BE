using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InfrastructureLayer.Cores.Database;

public static class DatabaseWriteErrorMapper
{
    public const string ConflictCode = "DATABASE_CONFLICT";
    public const string SchemaCode = "DATABASE_SCHEMA_ERROR";
    public const string WriteCode = "DATABASE_WRITE_ERROR";

    public sealed record MappedWriteError(
        int StatusCode,
        string ErrorCode,
        string Message,
        object Details);

    public static MappedWriteError Map(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException concurrency)
        {
            return new MappedWriteError(
                StatusCodesConflict,
                ConflictCode,
                "The request conflicts with existing data.",
                ConcurrencyDetails(concurrency));
        }

        var postgres = UnwrapPostgres(exception);
        if (postgres is null)
        {
            return new MappedWriteError(
                StatusCodesConflict,
                ConflictCode,
                "The request conflicts with existing data.",
                SafeDetails("update_failed", null));
        }

        var sqlState = postgres.SqlState ?? string.Empty;
        var details = SafeDetails(ClassifyReason(sqlState), postgres);
        return sqlState switch
        {
            "23505" or "23503" or "23514" or "23P01" => new MappedWriteError(
                StatusCodesConflict,
                ConflictCode,
                "The request conflicts with existing data.",
                details),
            "42703" or "42P01" or "42883" or "22P02" or "42804" or "23502" => new MappedWriteError(
                StatusCodesServerError,
                SchemaCode,
                "A database configuration issue occurred. Please contact the system administrator.",
                details),
            _ => new MappedWriteError(
                StatusCodesServerError,
                WriteCode,
                "A database write failed.",
                details)
        };
    }

    public static PostgresException? UnwrapPostgres(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
                return postgres;
        }

        return null;
    }

    private static object ConcurrencyDetails(DbUpdateConcurrencyException concurrency)
    {
        var entities = concurrency.Entries
            .Select(entry => new
            {
                entity = entry.Metadata.ClrType.Name,
                state = entry.State.ToString(),
                table = entry.Metadata.GetTableName()
            })
            .ToArray();
        return new
        {
            reason = "concurrency",
            sqlState = (string?)null,
            constraint = (string?)null,
            table = entities.FirstOrDefault()?.table,
            column = (string?)null,
            entities
        };
    }

    private static object SafeDetails(string reason, PostgresException? postgres)
        => new
        {
            reason,
            sqlState = postgres?.SqlState,
            constraint = postgres?.ConstraintName,
            table = postgres?.TableName,
            column = postgres?.ColumnName
        };

    private static string ClassifyReason(string sqlState)
        => sqlState switch
        {
            "23505" => "unique_violation",
            "23503" => "foreign_key_violation",
            "23514" => "check_violation",
            "23P01" => "exclusion_violation",
            "23502" => "not_null_violation",
            "42703" => "undefined_column",
            "42P01" => "undefined_table",
            "22P02" => "invalid_text_representation",
            "42804" => "datatype_mismatch",
            "42883" => "undefined_function",
            "22001" => "string_too_long",
            _ => "postgres_error"
        };

    private const int StatusCodesConflict = 409;
    private const int StatusCodesServerError = 500;
}
