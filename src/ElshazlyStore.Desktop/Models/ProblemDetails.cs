using System.Text.Json.Serialization;

namespace ElshazlyStore.Desktop.Models;

/// <summary>
/// Standard RFC 7807 ProblemDetails model for API error responses.
/// The backend puts error codes in the "title" field.
/// </summary>
public sealed class ProblemDetails
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("status")]
    public int? Status { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("instance")]
    public string? Instance { get; set; }

    [JsonPropertyName("errors")]
    public Dictionary<string, string[]>? Errors { get; set; }

    /// <summary>
    /// The backend error code, sourced from the Title field.
    /// Example: "BARCODE_ALREADY_EXISTS", "CUSTOMER_NOT_FOUND".
    /// </summary>
    public string? ErrorCode => Title;

    /// <summary>
    /// Returns a user-friendly Arabic error message using ErrorCodeMapper.
    /// Falls back to validation errors or Detail/Title if no mapping exists.
    /// </summary>
    public string ToUserMessage()
    {
        // Validation errors get concatenated
        if (Errors is { Count: > 0 })
        {
            var messages = Errors.SelectMany(e => e.Value).Where(message => !string.IsNullOrWhiteSpace(message)).Distinct();
            return string.Join(Environment.NewLine, messages);
        }

        return ErrorCodeMapper.ToArabicMessage(ErrorCode, Detail);
    }
}
