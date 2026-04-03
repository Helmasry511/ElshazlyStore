namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// A line on a production batch — either an input (raw material consumed)
/// or an output (finished good produced), distinguished by <see cref="LineType"/>.
/// </summary>
public sealed class ProductionBatchLine
{
    public Guid Id { get; set; }
    public Guid ProductionBatchId { get; set; }
    public ProductionLineType LineType { get; set; }
    public Guid VariantId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }

    // Navigation
    public ProductionBatch ProductionBatch { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}

/// <summary>
/// Distinguishes input (consumed) from output (produced) lines.
/// </summary>
public enum ProductionLineType
{
    Input = 0,
    Output = 1,
}
