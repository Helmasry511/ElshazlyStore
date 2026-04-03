using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using ElshazlyStore.Desktop.Models;
using Microsoft.Extensions.Logging;

namespace ElshazlyStore.Desktop.Services.Api;

/// <summary>
/// Typed HTTP client for communicating with the ElshazlyStore API.
/// Handles ProblemDetails parsing and standard error wrapping.
/// </summary>
public sealed class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiClient> _logger;

    private static readonly string TraceLogDir = Path.Combine(AppContext.BaseDirectory, "logs");
    private static readonly string TraceLogFile = Path.Combine(TraceLogDir, "api-trace.log");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ApiClient(HttpClient httpClient, ILogger<ApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResult<T>> GetAsync<T>(string endpoint, CancellationToken ct = default)
    {
        return await SendAsync<T>(HttpMethod.Get, endpoint, content: null, ct);
    }

    public async Task<ApiResult<T>> PostAsync<T>(string endpoint, object? body = null, CancellationToken ct = default)
    {
        var content = body is not null ? JsonContent.Create(body, options: JsonOptions) : null;
        return await SendAsync<T>(HttpMethod.Post, endpoint, content, ct);
    }

    public async Task<ApiResult<T>> PutAsync<T>(string endpoint, object? body = null, CancellationToken ct = default)
    {
        var content = body is not null ? JsonContent.Create(body, options: JsonOptions) : null;
        return await SendAsync<T>(HttpMethod.Put, endpoint, content, ct);
    }

    public async Task<ApiResult<T>> DeleteAsync<T>(string endpoint, CancellationToken ct = default)
    {
        return await SendAsync<T>(HttpMethod.Delete, endpoint, content: null, ct);
    }

    /// <summary>
    /// Upload a file as multipart/form-data (CP-3A: customer attachments).
    /// </summary>
    public async Task<ApiResult<T>> PostMultipartAsync<T>(
        string endpoint, string filePath, string fieldName = "file", CancellationToken ct = default)
    {
        var fileInfo = new System.IO.FileInfo(filePath);
        using var stream = fileInfo.OpenRead();
        var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(stream);

        // Determine content type from extension
        var ext = fileInfo.Extension.ToLowerInvariant();
        var mimeType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
        content.Add(fileContent, fieldName, fileInfo.Name);

        return await SendAsync<T>(HttpMethod.Post, endpoint, content, ct);
    }

    /// <summary>
    /// Download raw bytes from an endpoint (CP-3A: attachment download).
    /// </summary>
    public async Task<(byte[]? Data, string? FileName, string? Error)> GetBytesAsync(
        string endpoint, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            using var response = await _httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                    ?? "attachment";
                return (bytes, fileName, null);
            }
            var errorMessage = await TryParseProblemDetails(response, ct);
            return (null, null, errorMessage);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed: GET {Endpoint}", endpoint);
            return (null, null, Localization.Strings.State_ConnectionError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error on GET {Endpoint}", endpoint);
            return (null, null, Localization.Strings.State_UnexpectedError);
        }
    }

    private async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method,
        string endpoint,
        HttpContent? content,
        CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, endpoint) { Content = content };
            using var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var statusCode = (int)response.StatusCode;
                var body = await response.Content.ReadAsStringAsync(ct);
                try
                {
                    var data = JsonSerializer.Deserialize<T>(body, JsonOptions);
                    return ApiResult<T>.Success(data!, statusCode);
                }
                catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
                {
                    var excerpt = body.Length > 1000 ? body[..1000] + "…" : body;
                    var requestHeaders = RedactHeaders(request.Headers);
                    var responseHeaders = RedactHeaders(response.Headers);
                    var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] DESERIALIZE_ERROR\n" +
                                  $"  Request : {method} {endpoint}\n" +
                                  $"  Status  : {statusCode}\n" +
                                  $"  ReqHdrs : {requestHeaders}\n" +
                                  $"  ResHdrs : {responseHeaders}\n" +
                                  $"  Type<T> : {typeof(T).FullName}\n" +
                                  $"  Error   : {ex.Message}\n" +
                                  $"  Body    : {excerpt}";
                    WriteTraceLine(logLine);
                    _logger.LogError(ex, "Deserialization failed on {StatusCode} {Method} {Endpoint}", statusCode, method, endpoint);

                    return ApiResult<T>.Failure(Localization.Strings.State_UnexpectedError, statusCode);
                }
            }

            // Try to parse ProblemDetails from error response
            var errorMessage = await TryParseProblemDetails(response, ct);
            _logger.LogWarning("API error {StatusCode} on {Method} {Endpoint}: {Error}",
                (int)response.StatusCode, method, endpoint, errorMessage);

            return ApiResult<T>.Failure(errorMessage, (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed: {Method} {Endpoint}", method, endpoint);
            return ApiResult<T>.Failure(Localization.Strings.State_ConnectionError);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Request timed out: {Method} {Endpoint}", method, endpoint);
            return ApiResult<T>.Failure(Localization.Strings.State_TimeoutError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error on {Method} {Endpoint}", method, endpoint);
            return ApiResult<T>.Failure(Localization.Strings.State_UnexpectedError);
        }
    }

    private static async Task<string> TryParseProblemDetails(HttpResponseMessage response, CancellationToken ct)
    {
        var statusCode = (int)response.StatusCode;
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body))
                return GetFriendlyStatusMessage(statusCode);

            var problem = JsonSerializer.Deserialize<ProblemDetails>(body, JsonOptions);
            if (problem is not null)
                return problem.ToUserMessage();
        }
        catch
        {
            // Not JSON or parse error — fall through
        }

        return GetFriendlyStatusMessage(statusCode);
    }

    private static string GetFriendlyStatusMessage(int statusCode) => statusCode switch
    {
        400 or 422 => ErrorCodeMapper.ToArabicMessage("VALIDATION_FAILED"),
        401 => ErrorCodeMapper.ToArabicMessage("UNAUTHORIZED"),
        403 => ErrorCodeMapper.ToArabicMessage("FORBIDDEN"),
        404 => ErrorCodeMapper.ToArabicMessage("NOT_FOUND"),
        409 => ErrorCodeMapper.ToArabicMessage("CONFLICT"),
        _ => Localization.Strings.State_UnexpectedError,
    };

    private static void WriteTraceLine(string line)
    {
        try
        {
            Directory.CreateDirectory(TraceLogDir);
            File.AppendAllText(TraceLogFile, line + Environment.NewLine);
        }
        catch { }
    }

    /// <summary>Summarise headers for trace log, redacting Authorization.</summary>
    private static string RedactHeaders(System.Net.Http.Headers.HttpHeaders headers)
    {
        var parts = new List<string>();
        foreach (var h in headers)
        {
            var name = h.Key;
            var value = name.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                ? "[REDACTED]"
                : string.Join(", ", h.Value);
            parts.Add($"{name}: {value}");
        }
        return parts.Count > 0 ? string.Join(" | ", parts) : "(none)";
    }
}
