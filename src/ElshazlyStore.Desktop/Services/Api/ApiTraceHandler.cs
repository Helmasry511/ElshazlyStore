using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ElshazlyStore.Desktop.Services.Api;

/// <summary>
/// Lightweight DelegatingHandler that writes a one-line trace for every HTTP
/// request/response to logs/api-trace.log. Captures method, URL (sans token),
/// status code, elapsed ms, and ProblemDetails (title+detail) for non-2xx.
/// </summary>
public sealed class ApiTraceHandler : DelegatingHandler
{
    private static readonly string LogDir =
        Path.Combine(AppContext.BaseDirectory, "logs");
    private static readonly string LogFile =
        Path.Combine(LogDir, "api-trace.log");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        string? problemSnippet = null;

        try
        {
            response = await base.SendAsync(request, cancellationToken);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        using var doc = JsonDocument.Parse(body);
                        var root = doc.RootElement;
                        var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
                        var detail = root.TryGetProperty("detail", out var d) ? d.GetString() : null;
                        if (title is not null || detail is not null)
                            problemSnippet = $"title=\"{title}\" detail=\"{detail}\"";
                    }
                }
                catch { /* not JSON or parse error — ignore */ }
            }

            return response;
        }
        finally
        {
            sw.Stop();
            var status = response is not null ? (int)response.StatusCode : 0;
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {request.Method} {request.RequestUri?.PathAndQuery} → {status} ({sw.ElapsedMilliseconds}ms)";
            if (problemSnippet is not null)
                line += $"  {problemSnippet}";

            WriteLineAsync(line);
        }
    }

    private static void WriteLineAsync(string line)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            File.AppendAllText(LogFile, line + Environment.NewLine);
        }
        catch
        {
            // Tracing must never break the app
        }
    }
}
