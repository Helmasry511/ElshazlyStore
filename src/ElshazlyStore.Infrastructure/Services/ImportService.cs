using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElshazlyStore.Infrastructure.Services;

/// <summary>
/// Handles preview and commit of master data imports (CSV / XLSX).
/// </summary>
public sealed class ImportService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ImportService> _logger;
    private readonly AccountingService _accountingService;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ImportService(AppDbContext db, ILogger<ImportService> logger, AccountingService accountingService)
    {
        _db = db;
        _logger = logger;
        _accountingService = accountingService;
    }

    // ── Preview ────────────────────────────────────────────────

    public async Task<ImportPreviewResult> PreviewAsync(
        Stream fileStream, string fileName, string importType, Guid uploadedByUserId)
    {
        var fileBytes = await ReadAllBytesAsync(fileStream);
        var fileHash = ComputeHash(fileBytes);
        var rows = ParseFile(fileBytes, fileName, importType);

        var errors = await ValidateRowsAsync(rows, importType);

        var job = new ImportJob
        {
            Id = Guid.NewGuid(),
            Type = importType,
            FileName = fileName,
            FileHash = fileHash,
            UploadedByUserId = uploadedByUserId,
            Status = ImportJobStatus.Previewed,
            CreatedAtUtc = DateTime.UtcNow,
            FileContent = fileBytes,
        };

        var preview = new ImportPreviewResult
        {
            JobId = job.Id,
            TotalRows = rows.Count,
            ValidRows = rows.Count - errors.Count(e => e.Count > 0),
            RowErrors = errors,
        };

        job.PreviewResultJson = JsonSerializer.Serialize(preview, JsonOpts);
        _db.ImportJobs.Add(job);
        await _db.SaveChangesAsync();

        return preview;
    }

    // ── Commit ─────────────────────────────────────────────────

    public async Task<ImportCommitResult> CommitAsync(Guid jobId, Guid? commitUserId = null)
    {
        var job = await _db.ImportJobs.FindAsync(jobId);
        if (job is null)
            return ImportCommitResult.Fail("Import job not found.", "IMPORT_JOB_NOT_FOUND");

        if (job.Status == ImportJobStatus.Committed)
            return ImportCommitResult.Fail("Import job already committed.", "IMPORT_JOB_ALREADY_COMMITTED");

        var rows = ParseFile(job.FileContent, job.FileName, job.Type);
        var errors = await ValidateRowsAsync(rows, job.Type);

        if (errors.Any(e => e.Count > 0))
        {
            job.Status = ImportJobStatus.Failed;
            job.ErrorSummary = "Validation errors detected at commit time.";
            await _db.SaveChangesAsync();
            return ImportCommitResult.Fail("Validation errors exist. Cannot commit.", "IMPORT_COMMIT_FAILED");
        }

        try
        {
            switch (job.Type.ToLowerInvariant())
            {
                case "products":
                    await CommitProductsAsync(rows);
                    break;
                case "customers":
                    await CommitCustomersAsync(rows);
                    break;
                case "suppliers":
                    await CommitSuppliersAsync(rows);
                    break;
                case "opening_balances":
                    await CommitOpeningBalancesAsync(rows, commitUserId ?? job.UploadedByUserId);
                    break;
                case "payments":
                    await CommitPaymentsAsync(rows, commitUserId ?? job.UploadedByUserId);
                    break;
                default:
                    return ImportCommitResult.Fail($"Unknown import type '{job.Type}'.", "IMPORT_COMMIT_FAILED");
            }

            job.Status = ImportJobStatus.Committed;
            job.CommittedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Import job {JobId} committed: {Type}, {Count} rows",
                jobId, job.Type, rows.Count);

            return ImportCommitResult.Ok(rows.Count);
        }
        catch (Exception ex)
        {
            job.Status = ImportJobStatus.Failed;
            job.ErrorSummary = ex.Message.Length > 4000 ? ex.Message[..4000] : ex.Message;
            await _db.SaveChangesAsync();
            _logger.LogError(ex, "Import job {JobId} commit failed", jobId);
            return ImportCommitResult.Fail(ex.Message, "IMPORT_COMMIT_FAILED");
        }
    }

    // ── Parsing ────────────────────────────────────────────────

    private static List<Dictionary<string, string>> ParseFile(byte[] fileBytes, string fileName, string importType)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".csv" => ParseCsv(fileBytes),
            ".xlsx" => ParseXlsx(fileBytes),
            _ => throw new ArgumentException($"Unsupported file extension '{ext}'. Use .csv or .xlsx.")
        };
    }

    private static List<Dictionary<string, string>> ParseCsv(byte[] fileBytes)
    {
        var content = Encoding.UTF8.GetString(fileBytes);
        var lines = content.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return [];

        var headers = lines[0].Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();
        var rows = new List<Dictionary<string, string>>();

        for (var i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Split(',');
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var j = 0; j < headers.Length && j < values.Length; j++)
            {
                row[headers[j]] = values[j].Trim();
            }
            row["_row"] = (i + 1).ToString();
            rows.Add(row);
        }

        return rows;
    }

    private static List<Dictionary<string, string>> ParseXlsx(byte[] fileBytes)
    {
        using var stream = new MemoryStream(fileBytes);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();
        var rows = new List<Dictionary<string, string>>();

        var headerRow = worksheet.Row(1);
        var headers = new List<string>();
        for (var col = 1; col <= worksheet.LastColumnUsed()?.ColumnNumber(); col++)
        {
            headers.Add(headerRow.Cell(col).GetString().Trim().ToLowerInvariant());
        }

        for (var rowNum = 2; rowNum <= worksheet.LastRowUsed()?.RowNumber(); rowNum++)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var wsRow = worksheet.Row(rowNum);
            var hasData = false;

            for (var col = 0; col < headers.Count; col++)
            {
                var value = wsRow.Cell(col + 1).GetString().Trim();
                row[headers[col]] = value;
                if (!string.IsNullOrWhiteSpace(value)) hasData = true;
            }

            if (!hasData) continue;
            row["_row"] = rowNum.ToString();
            rows.Add(row);
        }

        return rows;
    }

    // ── Validation ─────────────────────────────────────────────

    private async Task<List<List<ImportRowError>>> ValidateRowsAsync(
        List<Dictionary<string, string>> rows, string importType)
    {
        return importType.ToLowerInvariant() switch
        {
            "products" => await ValidateProductRowsAsync(rows),
            "customers" => await ValidateCustomerRowsAsync(rows),
            "suppliers" => await ValidateSupplierRowsAsync(rows),
            "opening_balances" => await ValidateOpeningBalanceRowsAsync(rows),
            "payments" => await ValidatePaymentRowsAsync(rows),
            _ => rows.Select(r => new List<ImportRowError>
            {
                new() { Column = "_type", Message = $"Unknown import type '{importType}'." }
            }).ToList()
        };
    }

    private async Task<List<List<ImportRowError>>> ValidateProductRowsAsync(
        List<Dictionary<string, string>> rows)
    {
        var existingBarcodes = await _db.BarcodeReservations
            .Select(b => b.Barcode).ToHashSetAsync();
        var barcodesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skusSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existingSkus = await _db.ProductVariants
            .Select(v => v.Sku).ToHashSetAsync();

        var allErrors = new List<List<ImportRowError>>();

        foreach (var row in rows)
        {
            var errors = new List<ImportRowError>();
            var rowNum = Get(row, "_row", "?");

            if (!row.TryGetValue("productname", out var name) || string.IsNullOrWhiteSpace(name))
                errors.Add(new() { Column = "ProductName", Message = "Product name is required." });

            if (row.TryGetValue("sku", out var sku) && !string.IsNullOrWhiteSpace(sku))
            {
                if (existingSkus.Contains(sku) || !skusSeen.Add(sku))
                    errors.Add(new() { Column = "SKU", Message = $"Duplicate SKU '{sku}'." });
            }
            // If SKU is blank/missing, server will auto-generate during commit

            if (row.TryGetValue("barcode", out var barcode) && !string.IsNullOrWhiteSpace(barcode))
            {
                if (existingBarcodes.Contains(barcode) || !barcodesSeen.Add(barcode))
                    errors.Add(new() { Column = "Barcode", Message = $"Duplicate barcode '{barcode}'." });
            }

            if (row.TryGetValue("retailprice", out var rp) && !string.IsNullOrWhiteSpace(rp))
            {
                if (!decimal.TryParse(rp, CultureInfo.InvariantCulture, out _))
                    errors.Add(new() { Column = "RetailPrice", Message = "Invalid decimal value." });
            }

            if (row.TryGetValue("wholesaleprice", out var wp) && !string.IsNullOrWhiteSpace(wp))
            {
                if (!decimal.TryParse(wp, CultureInfo.InvariantCulture, out _))
                    errors.Add(new() { Column = "WholesalePrice", Message = "Invalid decimal value." });
            }

            allErrors.Add(errors);
        }

        return allErrors;
    }

    private async Task<List<List<ImportRowError>>> ValidateCustomerRowsAsync(
        List<Dictionary<string, string>> rows)
    {
        var existingCodes = await _db.Customers
            .Select(c => c.Code).ToHashSetAsync();
        var codesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var allErrors = new List<List<ImportRowError>>();

        foreach (var row in rows)
        {
            var errors = new List<ImportRowError>();

            if (!row.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
                errors.Add(new() { Column = "Name", Message = "Name is required." });

            if (row.TryGetValue("code", out var code) && !string.IsNullOrWhiteSpace(code))
            {
                if (existingCodes.Contains(code) || !codesSeen.Add(code))
                    errors.Add(new() { Column = "Code", Message = $"Duplicate code '{code}'." });
            }

            allErrors.Add(errors);
        }

        return allErrors;
    }

    private async Task<List<List<ImportRowError>>> ValidateSupplierRowsAsync(
        List<Dictionary<string, string>> rows)
    {
        var existingCodes = await _db.Suppliers
            .Select(s => s.Code).ToHashSetAsync();
        var codesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var allErrors = new List<List<ImportRowError>>();

        foreach (var row in rows)
        {
            var errors = new List<ImportRowError>();

            if (!row.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
                errors.Add(new() { Column = "Name", Message = "Name is required." });

            if (row.TryGetValue("code", out var code) && !string.IsNullOrWhiteSpace(code))
            {
                if (existingCodes.Contains(code) || !codesSeen.Add(code))
                    errors.Add(new() { Column = "Code", Message = $"Duplicate code '{code}'." });
            }

            allErrors.Add(errors);
        }

        return allErrors;
    }

    // ── Commit helpers ─────────────────────────────────────────

    /// <summary>Batch size for chunked SaveChanges during imports.</summary>
    private const int ImportBatchSize = 500;

    private async Task CommitProductsAsync(List<Dictionary<string, string>> rows)
    {
        // Group rows by product name to merge variants under the same parent
        var productGroups = rows.GroupBy(
            r => Get(r, "productname", "")!.Trim(),
            StringComparer.OrdinalIgnoreCase);

        var pending = 0;
        foreach (var group in productGroups)
        {
            var firstRow = group.First();

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = group.Key,
                Description = Get(firstRow, "description", null),
                Category = Get(firstRow, "category", null),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
            };
            _db.Products.Add(product);
            pending++;

            foreach (var row in group)
            {
                var variantId = Guid.NewGuid();
                var skuValue = Get(row, "sku", null);
                if (string.IsNullOrWhiteSpace(skuValue))
                    skuValue = await IdentifierGenerator.GenerateSkuAsync(_db);

                var variant = new ProductVariant
                {
                    Id = variantId,
                    ProductId = product.Id,
                    Sku = skuValue,
                    Color = Get(row, "color", null),
                    Size = Get(row, "size", null),
                    RetailPrice = TryParseDecimal(Get(row, "retailprice", null)),
                    WholesalePrice = TryParseDecimal(Get(row, "wholesaleprice", null)),
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow,
                };
                _db.ProductVariants.Add(variant);
                pending++;

                var barcodeValue = Get(row, "barcode", null);
                if (string.IsNullOrWhiteSpace(barcodeValue))
                    barcodeValue = IdentifierGenerator.GenerateBarcode();

                _db.BarcodeReservations.Add(new BarcodeReservation
                {
                    Id = Guid.NewGuid(),
                    Barcode = barcodeValue,
                    ReservedAtUtc = DateTime.UtcNow,
                    VariantId = variantId,
                    Status = BarcodeStatus.Assigned,
                });
                pending++;

                if (pending >= ImportBatchSize)
                {
                    await _db.SaveChangesAsync();
                    pending = 0;
                }
            }
        }

        if (pending > 0)
            await _db.SaveChangesAsync();
    }

    private async Task CommitCustomersAsync(List<Dictionary<string, string>> rows)
    {
        var maxCode = await _db.Customers
            .Where(c => c.Code.StartsWith("CUST-"))
            .Select(c => c.Code)
            .OrderByDescending(c => c)
            .FirstOrDefaultAsync();

        var nextSeq = 1;
        if (maxCode is not null && int.TryParse(maxCode.Replace("CUST-", ""), out var parsed))
            nextSeq = parsed + 1;

        // Generate CustomerCode sequence (YYYY-NNNNNN)
        var year = DateTime.UtcNow.Year.ToString();
        var custCodePrefix = $"{year}-";
        var maxCustCode = await _db.Customers
            .Where(c => c.CustomerCode.StartsWith(custCodePrefix))
            .Select(c => c.CustomerCode)
            .OrderByDescending(c => c)
            .FirstOrDefaultAsync();
        var nextCustCodeSeq = 1;
        if (maxCustCode is not null)
        {
            var seqPart = maxCustCode.Substring(custCodePrefix.Length);
            if (int.TryParse(seqPart, out var parsedCustCode))
                nextCustCodeSeq = parsedCustCode + 1;
        }

        var pending = 0;
        foreach (var row in rows)
        {
            var code = Get(row, "code", null);
            if (string.IsNullOrWhiteSpace(code))
                code = $"CUST-{nextSeq++:D6}";

            _db.Customers.Add(new Customer
            {
                Id = Guid.NewGuid(),
                Code = code,
                CustomerCode = $"{year}-{nextCustCodeSeq++:D6}",
                Name = Get(row, "name", "")!,
                Phone = Get(row, "phone", null),
                Phone2 = Get(row, "phone2", null),
                Notes = Get(row, "notes", null),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
            });
            pending++;

            if (pending >= ImportBatchSize)
            {
                await _db.SaveChangesAsync();
                pending = 0;
            }
        }

        if (pending > 0)
            await _db.SaveChangesAsync();
    }

    private async Task CommitSuppliersAsync(List<Dictionary<string, string>> rows)
    {
        var maxCode = await _db.Suppliers
            .Where(s => s.Code.StartsWith("SUP-"))
            .Select(s => s.Code)
            .OrderByDescending(s => s)
            .FirstOrDefaultAsync();

        var nextSeq = 1;
        if (maxCode is not null && int.TryParse(maxCode.Replace("SUP-", ""), out var parsed))
            nextSeq = parsed + 1;

        var pending = 0;
        foreach (var row in rows)
        {
            var code = Get(row, "code", null);
            if (string.IsNullOrWhiteSpace(code))
                code = $"SUP-{nextSeq++:D6}";

            _db.Suppliers.Add(new Supplier
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = Get(row, "name", "")!,
                Phone = Get(row, "phone", null),
                Phone2 = Get(row, "phone2", null),
                Notes = Get(row, "notes", null),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
            });
            pending++;

            if (pending >= ImportBatchSize)
            {
                await _db.SaveChangesAsync();
                pending = 0;
            }
        }

        if (pending > 0)
            await _db.SaveChangesAsync();
    }

    // ── Opening Balances Validation & Commit ───────────────────

    private async Task<List<List<ImportRowError>>> ValidateOpeningBalanceRowsAsync(
        List<Dictionary<string, string>> rows)
    {
        var customerCodes = await _db.Customers.Select(c => c.Code).ToHashSetAsync();
        var supplierCodes = await _db.Suppliers.Select(s => s.Code).ToHashSetAsync();

        var allErrors = new List<List<ImportRowError>>();

        foreach (var row in rows)
        {
            var errors = new List<ImportRowError>();

            // partytype: Customer or Supplier
            var partyType = Get(row, "partytype", null);
            if (string.IsNullOrWhiteSpace(partyType) ||
                (partyType.ToLowerInvariant() != "customer" && partyType.ToLowerInvariant() != "supplier"))
                errors.Add(new() { Column = "PartyType", Message = "PartyType is required (Customer or Supplier)." });

            // partycode: must exist
            var partyCode = Get(row, "partycode", null);
            if (string.IsNullOrWhiteSpace(partyCode))
            {
                errors.Add(new() { Column = "PartyCode", Message = "PartyCode is required." });
            }
            else if (!string.IsNullOrWhiteSpace(partyType))
            {
                if (partyType.ToLowerInvariant() == "customer" && !customerCodes.Contains(partyCode))
                    errors.Add(new() { Column = "PartyCode", Message = $"Customer code '{partyCode}' not found." });
                else if (partyType.ToLowerInvariant() == "supplier" && !supplierCodes.Contains(partyCode))
                    errors.Add(new() { Column = "PartyCode", Message = $"Supplier code '{partyCode}' not found." });
            }

            // amount: required, positive decimal
            var amountStr = Get(row, "amount", null);
            if (string.IsNullOrWhiteSpace(amountStr))
            {
                errors.Add(new() { Column = "Amount", Message = "Amount is required." });
            }
            else if (!decimal.TryParse(amountStr, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            {
                errors.Add(new() { Column = "Amount", Message = "Amount must be a positive number." });
            }

            allErrors.Add(errors);
        }

        return allErrors;
    }

    private async Task CommitOpeningBalancesAsync(List<Dictionary<string, string>> rows, Guid userId)
    {
        var customers = await _db.Customers.ToDictionaryAsync(c => c.Code, c => c.Id);
        var suppliers = await _db.Suppliers.ToDictionaryAsync(s => s.Code, s => s.Id);

        foreach (var row in rows)
        {
            var partyType = Get(row, "partytype", "")!.Trim().ToLowerInvariant();
            var partyCode = Get(row, "partycode", "")!.Trim();
            var amount = decimal.Parse(Get(row, "amount", "0")!, CultureInfo.InvariantCulture);
            var reference = Get(row, "reference", null);

            if (partyType == "customer" && customers.TryGetValue(partyCode, out var customerId))
            {
                await _accountingService.CreateOpeningBalanceEntryAsync(
                    PartyType.Customer, customerId, amount, reference, userId);
            }
            else if (partyType == "supplier" && suppliers.TryGetValue(partyCode, out var supplierId))
            {
                await _accountingService.CreateOpeningBalanceEntryAsync(
                    PartyType.Supplier, supplierId, amount, reference, userId);
            }
        }
    }

    // ── Payments Validation & Commit ───────────────────────────

    private async Task<List<List<ImportRowError>>> ValidatePaymentRowsAsync(
        List<Dictionary<string, string>> rows)
    {
        var customerCodes = await _db.Customers.Select(c => c.Code).ToHashSetAsync();
        var supplierCodes = await _db.Suppliers.Select(s => s.Code).ToHashSetAsync();
        var validMethods = new[] { "cash", "instapay", "ewallet", "visa" };

        var allErrors = new List<List<ImportRowError>>();

        foreach (var row in rows)
        {
            var errors = new List<ImportRowError>();

            // partytype
            var partyType = Get(row, "partytype", null);
            if (string.IsNullOrWhiteSpace(partyType) ||
                (partyType.ToLowerInvariant() != "customer" && partyType.ToLowerInvariant() != "supplier"))
                errors.Add(new() { Column = "PartyType", Message = "PartyType is required (Customer or Supplier)." });

            // partycode
            var partyCode = Get(row, "partycode", null);
            if (string.IsNullOrWhiteSpace(partyCode))
            {
                errors.Add(new() { Column = "PartyCode", Message = "PartyCode is required." });
            }
            else if (!string.IsNullOrWhiteSpace(partyType))
            {
                if (partyType.ToLowerInvariant() == "customer" && !customerCodes.Contains(partyCode))
                    errors.Add(new() { Column = "PartyCode", Message = $"Customer code '{partyCode}' not found." });
                else if (partyType.ToLowerInvariant() == "supplier" && !supplierCodes.Contains(partyCode))
                    errors.Add(new() { Column = "PartyCode", Message = $"Supplier code '{partyCode}' not found." });
            }

            // amount
            var amountStr = Get(row, "amount", null);
            if (string.IsNullOrWhiteSpace(amountStr))
            {
                errors.Add(new() { Column = "Amount", Message = "Amount is required." });
            }
            else if (!decimal.TryParse(amountStr, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            {
                errors.Add(new() { Column = "Amount", Message = "Amount must be a positive number." });
            }

            // method
            var method = Get(row, "method", null);
            if (string.IsNullOrWhiteSpace(method))
            {
                errors.Add(new() { Column = "Method", Message = "Payment method is required (Cash, InstaPay, EWallet, Visa)." });
            }
            else if (!validMethods.Contains(method.ToLowerInvariant()))
            {
                errors.Add(new() { Column = "Method", Message = $"Invalid method '{method}'. Use Cash, InstaPay, EWallet, or Visa." });
            }

            // walletname required for EWallet
            if (!string.IsNullOrWhiteSpace(method) && method.ToLowerInvariant() == "ewallet")
            {
                var walletName = Get(row, "walletname", null);
                if (string.IsNullOrWhiteSpace(walletName))
                    errors.Add(new() { Column = "WalletName", Message = "WalletName is required for EWallet payments." });
            }

            allErrors.Add(errors);
        }

        return allErrors;
    }

    private async Task CommitPaymentsAsync(List<Dictionary<string, string>> rows, Guid userId)
    {
        var customers = await _db.Customers.ToDictionaryAsync(c => c.Code, c => c.Id);
        var suppliers = await _db.Suppliers.ToDictionaryAsync(s => s.Code, s => s.Id);

        foreach (var row in rows)
        {
            var partyTypeStr = Get(row, "partytype", "")!.Trim().ToLowerInvariant();
            var partyCode = Get(row, "partycode", "")!.Trim();
            var amount = decimal.Parse(Get(row, "amount", "0")!, CultureInfo.InvariantCulture);
            var methodStr = Get(row, "method", "cash")!.Trim().ToLowerInvariant();
            var walletName = Get(row, "walletname", null);
            var reference = Get(row, "reference", null);

            var partyType = partyTypeStr == "customer" ? PartyType.Customer : PartyType.Supplier;
            Guid partyId;

            if (partyType == PartyType.Customer)
                partyId = customers[partyCode];
            else
                partyId = suppliers[partyCode];

            var method = methodStr switch
            {
                "instapay" => PaymentMethod.InstaPay,
                "ewallet" => PaymentMethod.EWallet,
                "visa" => PaymentMethod.Visa,
                _ => PaymentMethod.Cash,
            };

            // Create payment (allow overpay for imported historical payments)
            var request = new AccountingService.CreatePaymentRequest(
                partyType, partyId, amount, method, walletName, reference, null, null);

            await _accountingService.CreatePaymentAsync(request, userId, allowOverpay: true);
        }
    }

    // ── Helpers ────────────────────────────────────────────────

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    private static string ComputeHash(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // Barcode generation now delegated to IdentifierGenerator.GenerateBarcode()

    private static decimal? TryParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return decimal.TryParse(value, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    /// <summary>Gets a value from a row dictionary, returning defaultValue if not found.</summary>
    private static string? Get(Dictionary<string, string> row, string key, string? defaultValue = null)
    {
        return row.TryGetValue(key, out var value) ? value : defaultValue;
    }
}

// ── DTOs ───────────────────────────────────────────────────

/// <summary>Result of an import preview operation.</summary>
public sealed class ImportPreviewResult
{
    public Guid JobId { get; set; }
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public List<List<ImportRowError>> RowErrors { get; set; } = [];
}

/// <summary>Per-row, per-column import error.</summary>
public sealed class ImportRowError
{
    public string Column { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>Result of an import commit operation.</summary>
public sealed class ImportCommitResult
{
    public bool Success { get; set; }
    public int ImportedCount { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }

    public static ImportCommitResult Ok(int count) => new() { Success = true, ImportedCount = count };
    public static ImportCommitResult Fail(string message, string code) =>
        new() { Success = false, ErrorMessage = message, ErrorCode = code };
}

/// <summary>
/// Extension to build HashSet from EF query.
/// </summary>
internal static class QueryableExtensions
{
    public static async Task<HashSet<T>> ToHashSetAsync<T>(
        this IQueryable<T> source, CancellationToken ct = default)
    {
        var list = await source.ToListAsync(ct);
        return [.. list];
    }
}
