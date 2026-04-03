using ElshazlyStore.Desktop.Models.Dtos;

namespace ElshazlyStore.Desktop.Helpers;

/// <summary>
/// Shared sales warehouse policy used by Sales Admin and POS.
/// </summary>
public static class SalesWarehousePolicy
{
    private static readonly string[] NonSaleWarehouseMarkers =
    [
        "عزل",
        "تالف",
        "هالك",
        "إتلاف",
        "مرتجع",
        "مرتجعات",
        "فحص",
        "quarantine",
        "scrap",
        "damaged",
        "damage",
        "return",
        "returns",
        "rework",
        "writeoff",
        "write-off"
    ];

    public static IReadOnlyList<WarehouseDto> BuildSalesWarehouses(
        IEnumerable<WarehouseDto> activeWarehouses,
        Guid? requiredWarehouseId = null)
    {
        var source = activeWarehouses.ToList();

        var filtered = source
            .Where(IsLikelySalesWarehouse)
            .OrderByDescending(warehouse => warehouse.IsDefault)
            .ThenBy(warehouse => warehouse.Name)
            .ToList();

        if (filtered.Count == 0)
        {
            filtered = source
                .OrderByDescending(warehouse => warehouse.IsDefault)
                .ThenBy(warehouse => warehouse.Name)
                .ToList();
        }

        if (requiredWarehouseId.HasValue && filtered.All(warehouse => warehouse.Id != requiredWarehouseId.Value))
        {
            var required = source.FirstOrDefault(warehouse => warehouse.Id == requiredWarehouseId.Value);
            if (required is not null)
                filtered.Insert(0, required);
        }

        return filtered
            .DistinctBy(warehouse => warehouse.Id)
            .ToList();
    }

    private static bool IsLikelySalesWarehouse(WarehouseDto warehouse)
    {
        var haystack = $"{warehouse.Code} {warehouse.Name}".Trim().ToLowerInvariant();
        return NonSaleWarehouseMarkers.All(marker => !haystack.Contains(marker));
    }
}
