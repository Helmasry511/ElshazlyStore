using System.Text.Json.Serialization;

namespace ElshazlyStore.Desktop.Models.Auth;

public sealed class MeResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = [];
}
