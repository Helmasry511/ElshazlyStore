namespace ElshazlyStore.Desktop.Models.Dtos;

public sealed class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int VariantCount { get; set; }
    public Guid? DefaultWarehouseId { get; set; }
    public string? DefaultWarehouseName { get; set; }
}

public sealed class ProductDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? DefaultWarehouseId { get; set; }
    public string? DefaultWarehouseName { get; set; }
    public List<VariantDto> Variants { get; set; } = [];
}

public sealed class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public Guid? DefaultWarehouseId { get; set; }
}

public sealed class UpdateProductRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool? IsActive { get; set; }
    /// <summary>
    /// Null = don't change, Guid.Empty = clear, valid GUID = set default warehouse.
    /// </summary>
    public Guid? DefaultWarehouseId { get; set; }
}
