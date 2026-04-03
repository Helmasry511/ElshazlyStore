namespace ElshazlyStore.Desktop.Models.Dtos;

public sealed class WarehouseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Display format: "Code — Name". For the AllWarehouses sentinel (Code empty), returns Name only.</summary>
    public string DisplayText => string.IsNullOrEmpty(Code) ? Name : $"{Code} — {Name}";
}

public sealed class CreateWarehouseRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool IsDefault { get; set; }
}

public sealed class UpdateWarehouseRequest
{
    public string? Name { get; set; }
    public string? Address { get; set; }
    public bool? IsActive { get; set; }
}
