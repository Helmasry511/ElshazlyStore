namespace ElshazlyStore.Desktop.Models;

/// <summary>
/// Generic paged response model matching the backend's PagedResult JSON shape.
/// Both Shape A (PagedResult&lt;T&gt;) and Shape B (anonymous) serialize to this identical JSON.
/// </summary>
public sealed class PagedResponse<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }

    /// <summary>Total number of pages based on TotalCount and PageSize.</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
