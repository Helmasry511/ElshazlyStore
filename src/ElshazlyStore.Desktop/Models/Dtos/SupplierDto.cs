namespace ElshazlyStore.Desktop.Models.Dtos;

public sealed class SupplierDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Phone2 { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class CreateSupplierRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Phone { get; set; }
    public string? Phone2 { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpdateSupplierRequest
{
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Phone { get; set; }
    public string? Phone2 { get; set; }
    public string? Notes { get; set; }
    public bool? IsActive { get; set; }
}
