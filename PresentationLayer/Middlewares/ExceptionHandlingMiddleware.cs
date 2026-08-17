using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using InfrastructureLayer.Cores.Database;
using Microsoft.EntityFrameworkCore;

namespace PresentationLayer.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException exception)
        {
            await WriteErrorAsync(context, exception, exception.StatusCode, exception.ErrorCode, exception.Message, exception.Details, exception.FieldErrors);
        }
        catch (DbUpdateException exception)
        {
            var mapped = DatabaseWriteErrorMapper.Map(exception);
            var postgres = DatabaseWriteErrorMapper.UnwrapPostgres(exception);
            if (exception is DbUpdateConcurrencyException concurrencyException)
            {
                _logger.LogWarning(
                    "Database concurrency conflict for entities {EntityTypes}.",
                    string.Join(",", concurrencyException.Entries.Select(entry => entry.Metadata.ClrType.Name)));
            }
            else if (postgres is not null)
            {
                _logger.LogWarning(
                    "Database write failed. SqlState={SqlState} Constraint={Constraint} Table={Table} Column={Column} Mapped={ErrorCode}",
                    postgres.SqlState,
                    postgres.ConstraintName,
                    postgres.TableName,
                    postgres.ColumnName,
                    mapped.ErrorCode);
            }

            await WriteErrorAsync(
                context,
                exception,
                mapped.StatusCode,
                mapped.ErrorCode,
                mapped.Message,
                mapped.Details);
        }
        catch (Npgsql.PostgresException exception)
        {
            var mapped = DatabaseWriteErrorMapper.Map(exception);
            _logger.LogWarning(
                "Database postgres error. SqlState={SqlState} Constraint={Constraint} Table={Table} Column={Column} Mapped={ErrorCode}",
                exception.SqlState,
                exception.ConstraintName,
                exception.TableName,
                exception.ColumnName,
                mapped.ErrorCode);
            await WriteErrorAsync(
                context,
                exception,
                mapped.StatusCode,
                mapped.ErrorCode,
                mapped.Message,
                mapped.Details);
        }
        catch (Exception exception)
        {
            await WriteErrorAsync(
                context,
                exception,
                StatusCodes.Status500InternalServerError,
                "INTERNAL_SERVER_ERROR",
                "An unexpected error occurred.");
        }
    }

    private async Task WriteErrorAsync(
        HttpContext context,
        Exception exception,
        int statusCode,
        string errorCode,
        string message,
        object? details = null,
        Dictionary<string, string[]>? fieldErrors = null)
    {
        var traceId = context.TraceIdentifier;
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Request {Method} {Path} failed with trace id {TraceId}",
                context.Request.Method,
                context.Request.Path.Value,
                traceId);
        }
        else
        {
            _logger.LogInformation(
                "Request {Method} {Path} returned {StatusCode} ({ErrorCode}) with trace id {TraceId}: {Message}",
                context.Request.Method,
                context.Request.Path.Value,
                statusCode,
                errorCode,
                traceId,
                message);
        }

        if (context.Response.HasStarted)
        {
            throw exception;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = ApiResponse<ErrorResponse>.Failure(
            message,
            errorCode,
            new ErrorResponse
        {
            TraceId = traceId,
            ErrorCode = errorCode,
            Details = details,
            FieldErrors = fieldErrors
        });

        await context.Response.WriteAsJsonAsync(response);
    }
}
