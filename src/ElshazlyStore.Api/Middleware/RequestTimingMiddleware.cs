using System.Diagnostics;

namespace ElshazlyStore.Api.Middleware;

/// <summary>
/// Logs requests that exceed a configurable duration threshold.
/// Includes route, method, status code, and correlation ID.
/// </summary>
public sealed class RequestTimingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimingMiddleware> _logger;
    private readonly int _thresholdMs;

    public RequestTimingMiddleware(
        RequestDelegate next,
        ILogger<RequestTimingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _thresholdMs = configuration.GetValue<int>("Performance:SlowRequestThresholdMs", 500);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        await _next(context);
        sw.Stop();

        if (sw.ElapsedMilliseconds > _thresholdMs)
        {
            context.Items.TryGetValue("CorrelationId", out var correlationId);
            _logger.LogWarning(
                "Slow request: {Method} {Path} → {StatusCode} in {ElapsedMs}ms [CID:{CorrelationId}]",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds,
                correlationId ?? "N/A");
        }
    }
}
