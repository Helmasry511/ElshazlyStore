using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Api.Middleware;

/// <summary>
/// Catches all unhandled exceptions and returns RFC 7807 ProblemDetails
/// with a stable error code. Logs the full exception server-side.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — suppress, no response needed
            _logger.LogDebug("Request cancelled by client on {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemDetailsAsync(context, ex);
        }
    }

    private static async Task WriteProblemDetailsAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorCode, detail) = ClassifyException(exception);

        // In non-Development environments, mask internal error messages to prevent information leakage
        var env = context.RequestServices.GetService<IHostEnvironment>();
        var safeDetail = statusCode >= 500 && env?.IsDevelopment() != true
            ? "An internal error occurred. Check server logs for details."
            : detail ?? exception.Message;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = errorCode,
            Detail = safeDetail,
            Instance = context.Request.Path,
            Type = $"https://elshazlystore.local/errors/{errorCode.ToLowerInvariant()}"
        };

        // Attach correlation id if present
        if (context.Items.TryGetValue("CorrelationId", out var correlationId))
        {
            problem.Extensions["correlationId"] = correlationId;
        }

        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsJsonAsync(problem, options);
    }

    private static (int StatusCode, string ErrorCode, string? Detail) ClassifyException(Exception exception)
    {
        return exception switch
        {
            ArgumentException argEx => ((int)HttpStatusCode.BadRequest, Domain.Common.ErrorCodes.ValidationFailed, argEx.Message),
            KeyNotFoundException notFoundEx => ((int)HttpStatusCode.NotFound, Domain.Common.ErrorCodes.NotFound, notFoundEx.Message),
            UnauthorizedAccessException forbiddenEx => ((int)HttpStatusCode.Forbidden, Domain.Common.ErrorCodes.Forbidden, forbiddenEx.Message),
            DbUpdateException dbUpdateEx => ClassifyDbUpdateException(dbUpdateEx),
            _ => ((int)HttpStatusCode.InternalServerError, Domain.Common.ErrorCodes.InternalError, GetInnermostMessage(exception))
        };
    }

    private static (int StatusCode, string ErrorCode, string? Detail) ClassifyDbUpdateException(DbUpdateException exception)
    {
        var detail = GetInnermostMessage(exception);
        var postgresException = FindInnerException<Npgsql.PostgresException>(exception);
        if (postgresException is not null && postgresException.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
            return ((int)HttpStatusCode.Conflict, Domain.Common.ErrorCodes.Conflict, detail);

        if (detail.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("unique constraint", StringComparison.OrdinalIgnoreCase))
        {
            return ((int)HttpStatusCode.Conflict, Domain.Common.ErrorCodes.Conflict, detail);
        }

        return ((int)HttpStatusCode.InternalServerError, Domain.Common.ErrorCodes.InternalError, detail);
    }

    private static TException? FindInnerException<TException>(Exception exception)
        where TException : Exception
    {
        var current = exception;
        while (current is not null)
        {
            if (current is TException match)
                return match;

            current = current.InnerException!;
        }

        return null;
    }

    private static string GetInnermostMessage(Exception exception)
    {
        var current = exception;
        while (current.InnerException is not null)
            current = current.InnerException;

        return current.Message;
    }
}
