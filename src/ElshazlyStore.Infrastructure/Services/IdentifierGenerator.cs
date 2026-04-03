using System.Text;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Infrastructure.Services;

/// <summary>
/// Server-side generation of variant identifiers (SKU + Barcode).
/// SKU: 10-digit numeric, counter-based (queries max existing numeric SKU and increments).
/// Barcode: 13-digit numeric, random (EAN-like).
/// Both are guaranteed unique by DB constraints; callers must implement a retry loop
/// on <see cref="Microsoft.EntityFrameworkCore.DbUpdateException"/> for concurrency safety.
/// </summary>
public static class IdentifierGenerator
{
    /// <summary>
    /// Generate a new 10-digit numeric SKU by finding the current max numeric SKU
    /// in the database and incrementing by 1.
    /// <para>
    /// Strategy: load only SKUs that look numeric (10-digit, zero-padded) into memory,
    /// then find the max. Falls back to scanning all SKUs if none match the pattern.
    /// This avoids loading millions of non-numeric custom SKUs.
    /// </para>
    /// </summary>
    public static async Task<string> GenerateSkuAsync(AppDbContext db)
    {
        // Step 1: Load candidate numeric SKUs (exactly 10 chars, all digits).
        // EF translates Length to SQL LENGTH(); the digit-check must happen in memory
        // because EF Core cannot translate Char.IsDigit / regex to all providers.
        var candidates = await db.ProductVariants
            .Where(v => v.Sku.Length == 10)
            .Select(v => v.Sku)
            .ToListAsync();

        long maxNumeric = 0;
        foreach (var s in candidates)
        {
            if (long.TryParse(s, out var val) && val > maxNumeric)
                maxNumeric = val;
        }

        // Step 2: If no 10-digit numeric SKUs exist, scan ALL SKUs for any numeric value.
        // This covers databases with shorter numeric SKUs from older imports.
        if (maxNumeric == 0)
        {
            var allSkus = await db.ProductVariants
                .Where(v => v.Sku.Length > 0)
                .Select(v => v.Sku)
                .ToListAsync();

            foreach (var s in allSkus)
            {
                if (long.TryParse(s, out var val) && val > maxNumeric)
                    maxNumeric = val;
            }
        }

        var next = maxNumeric + 1;
        return next.ToString("D10");
    }

    /// <summary>
    /// Generate a 13-digit random numeric barcode (EAN-like format).
    /// </summary>
    public static string GenerateBarcode()
    {
        var random = Random.Shared;
        var sb = new StringBuilder(13);
        for (var i = 0; i < 13; i++)
            sb.Append(random.Next(0, 10));
        return sb.ToString();
    }
}
