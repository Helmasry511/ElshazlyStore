namespace ElshazlyStore.Desktop.Models;

/// <summary>
/// Generic wrapper for API call results, used by ApiClient.
/// </summary>
public sealed class ApiResult<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public int StatusCode { get; init; }

    public static ApiResult<T> Success(T data, int statusCode = 200) =>
        new() { IsSuccess = true, Data = data, StatusCode = statusCode };

    public static ApiResult<T> Failure(string error, int statusCode = 0) =>
        new() { IsSuccess = false, ErrorMessage = error, StatusCode = statusCode };
}
