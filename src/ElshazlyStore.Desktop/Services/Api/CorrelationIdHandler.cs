using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ElshazlyStore.Desktop.Services.Api;

/// <summary>
/// Adds a Correlation-Id header to every outgoing request for traceability.
/// </summary>
public sealed class CorrelationIdHandler : DelegatingHandler
{
    private const string HeaderName = "X-Correlation-Id";
    private readonly ILogger<CorrelationIdHandler> _logger;

    public CorrelationIdHandler(ILogger<CorrelationIdHandler> logger)
    {
        _logger = logger;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        request.Headers.TryAddWithoutValidation(HeaderName, correlationId);
        _logger.LogDebug("Outgoing request {Method} {Uri} — CorrelationId: {CorrelationId}",
            request.Method, request.RequestUri, correlationId);

        return base.SendAsync(request, cancellationToken);
    }
}
