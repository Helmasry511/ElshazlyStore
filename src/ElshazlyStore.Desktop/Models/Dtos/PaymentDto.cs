using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElshazlyStore.Desktop.Models.Dtos;

public sealed class PaymentDto
{
    public Guid Id { get; set; }
    public string? PaymentNumber { get; set; }

    /// <summary>
    /// Server may serialize the PartyType enum as an integer (0=Customer,1=Supplier).
    /// FlexibleStringJsonConverter ensures deserialization succeeds for both int and string values.
    /// </summary>
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? PartyType { get; set; }

    public Guid PartyId { get; set; }
    public string? PartyName { get; set; }
    public decimal Amount { get; set; }
    public string? Method { get; set; }
    public string? WalletName { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PaymentDateUtc { get; set; }

    // Fields the server returns but Desktop didn't have — nullable to avoid crash if absent
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByUsername { get; set; }

    public string MethodDisplay => Method switch
    {
        "Cash" => "نقدي",
        "Visa" => "فيزا",
        "InstaPay" => "إنستاباي",
        "EWallet" => WalletName ?? "محفظة",
        _ => WalletName ?? Method ?? "—"
    };

    public string DateDisplay => (PaymentDateUtc ?? CreatedAtUtc).ToString("yyyy-MM-dd");
}

/// <summary>
/// Reads JSON strings, numbers, or booleans as a C# string.
/// Used when the server may serialize enums as integers or strings.
/// </summary>
internal sealed class FlexibleStringJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var l)
                ? l.ToString(CultureInfo.InvariantCulture)
                : reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => null,
            _ => null
        };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value);
    }
}

public sealed class CreatePaymentRequest
{
    public string? PartyType { get; set; }
    public Guid PartyId { get; set; }
    public decimal Amount { get; set; }
    public string? Method { get; set; }
    public string? WalletName { get; set; }
    public string? Reference { get; set; }
    public Guid? RelatedInvoiceId { get; set; }
    public DateTime? PaymentDateUtc { get; set; }
}
