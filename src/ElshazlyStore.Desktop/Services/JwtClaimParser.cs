using System.Text;
using System.Text.Json;

namespace ElshazlyStore.Desktop.Services;

/// <summary>
/// Extracts permission claims from a JWT access token without requiring 
/// a full JWT library. Parses the Base64-encoded payload.
/// </summary>
public static class JwtClaimParser
{
    private const string PermissionClaimType = "permission";

    /// <summary>
    /// Extracts all "permission" claims from the JWT payload.
    /// </summary>
    public static List<string> ExtractPermissions(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3)
            return [];

        var payloadJson = DecodeBase64Url(parts[1]);
        using var doc = JsonDocument.Parse(payloadJson);

        var permissions = new List<string>();
        if (doc.RootElement.TryGetProperty(PermissionClaimType, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.EnumerateArray())
                {
                    var val = item.GetString();
                    if (!string.IsNullOrEmpty(val))
                        permissions.Add(val);
                }
            }
            else if (prop.ValueKind == JsonValueKind.String)
            {
                var val = prop.GetString();
                if (!string.IsNullOrEmpty(val))
                    permissions.Add(val);
            }
        }

        return permissions;
    }

    private static string DecodeBase64Url(string base64Url)
    {
        var padded = base64Url
            .Replace('-', '+')
            .Replace('_', '/');

        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        var bytes = Convert.FromBase64String(padded);
        return Encoding.UTF8.GetString(bytes);
    }
}
