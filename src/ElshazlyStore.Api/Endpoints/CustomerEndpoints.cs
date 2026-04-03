using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Infrastructure.Extensions;
using ElshazlyStore.Infrastructure.Persistence;
using ElshazlyStore.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Customer CRUD with paging, sorting, and search across code/name/phone.
/// CP-3A: Credit profile fields + customer attachments.
/// </summary>
public static class CustomerEndpoints
{
    private const long MaxAttachmentSize = 5 * 1024 * 1024; // 5 MB
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf", ".doc", ".docx" };

    public static RouteGroupBuilder MapCustomerEndpoints(this RouteGroupBuilder group)
    {
        var customers = group.MapGroup("/customers").WithTags("Customers");

        customers.MapGet("/", ListAsync)
            .RequireAuthorization($"Permission:{Permissions.CustomersRead}");
        customers.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization($"Permission:{Permissions.CustomersRead}");
        customers.MapPost("/", CreateAsync)
            .RequireAuthorization($"Permission:{Permissions.CustomersWrite}");
        customers.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization($"Permission:{Permissions.CustomersWrite}");
        customers.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization($"Permission:{Permissions.CustomersWrite}");

        // CAFS-1-R1: Attachments folder path for desktop "Open Folder" action
        customers.MapGet("/{id:guid}/attachments-folder", GetAttachmentsFolderAsync)
            .RequireAuthorization($"Permission:{Permissions.CustomersRead}");

        // CP-3A: Customer attachments
        customers.MapGet("/{id:guid}/attachments", ListAttachmentsAsync)
            .RequireAuthorization($"Permission:{Permissions.CustomersRead}");
        customers.MapPost("/{id:guid}/attachments", UploadAttachmentAsync)
            .RequireAuthorization($"Permission:{Permissions.CustomersWrite}")
            .DisableAntiforgery();
        customers.MapGet("/{id:guid}/attachments/{attachmentId:guid}", DownloadAttachmentAsync)
            .RequireAuthorization($"Permission:{Permissions.CustomersRead}");
        customers.MapDelete("/{id:guid}/attachments/{attachmentId:guid}", DeleteAttachmentAsync)
            .RequireAuthorization($"Permission:{Permissions.CustomersWrite}");

        return group;
    }

    private static async Task<IResult> ListAsync(
        AppDbContext db,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? sort = "name",
        [FromQuery] bool includeTotal = true)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        IQueryable<Customer> query = db.Customers;

        query = query.ApplySearch(db.Database, q,
            c => c.Code,
            c => c.Name,
            c => c.Phone,
            c => c.Phone2);

        query = sort?.ToLowerInvariant() switch
        {
            "name" => query.OrderBy(c => c.Name),
            "name_desc" => query.OrderByDescending(c => c.Name),
            "code" => query.OrderBy(c => c.Code),
            "created" => query.OrderBy(c => c.CreatedAtUtc),
            "created_desc" => query.OrderByDescending(c => c.CreatedAtUtc),
            _ => query.OrderBy(c => c.Name),
        };

        var totalCount = includeTotal ? await query.CountAsync() : -1;
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerDto(c.Id, c.Code, c.CustomerCode, c.Name, c.Phone, c.Phone2, c.Notes,
                c.WhatsApp, c.WalletNumber, c.InstaPayId,
                c.CommercialName, c.CommercialAddress, c.NationalIdNumber,
                c.IsActive, c.CreatedAtUtc))
            .ToListAsync();

        return Results.Ok(new PagedResult<CustomerDto>(items, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db)
    {
        var customer = await db.Customers.FindAsync(id);
        if (customer is null)
            return Problem(404, ErrorCodes.NotFound, "Customer not found.");

        return Results.Ok(new CustomerDto(customer.Id, customer.Code, customer.CustomerCode, customer.Name,
            customer.Phone, customer.Phone2, customer.Notes,
            customer.WhatsApp, customer.WalletNumber, customer.InstaPayId,
            customer.CommercialName, customer.CommercialAddress, customer.NationalIdNumber,
            customer.IsActive, customer.CreatedAtUtc));
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateCustomerRequest req, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Problem(400, ErrorCodes.ValidationFailed, "Customer name is required.");

        var code = req.Code;
        if (string.IsNullOrWhiteSpace(code))
        {
            // Server-generate code
            var maxCode = await db.Customers
                .Where(c => c.Code.StartsWith("CUST-"))
                .Select(c => c.Code)
                .OrderByDescending(c => c)
                .FirstOrDefaultAsync();

            var nextSeq = 1;
            if (maxCode is not null && int.TryParse(maxCode.Replace("CUST-", ""), out var parsed))
                nextSeq = parsed + 1;

            code = $"CUST-{nextSeq:D6}";
        }
        else
        {
            // CAFS-1-R1: Validate Code contains only digits, letters, and hyphens (no forbidden filesystem chars)
            foreach (var ch in code)
            {
                if (!char.IsLetterOrDigit(ch) && ch != '-')
                    return Problem(400, ErrorCodes.ValidationFailed,
                        $"Customer code contains invalid character '{ch}'. Only letters, digits, and '-' are allowed.");
            }

            if (await db.Customers.AnyAsync(c => c.Code == code))
                return Problem(409, ErrorCodes.Conflict, $"Customer code '{code}' already exists.");
        }

        // Generate immutable global CustomerCode (YYYY-NNNNNN format)
        var customerCode = await GenerateCustomerCodeAsync(db);

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Code = code,
            CustomerCode = customerCode,
            Name = req.Name,
            Phone = req.Phone,
            Phone2 = req.Phone2,
            Notes = req.Notes,
            WhatsApp = req.WhatsApp,
            WalletNumber = req.WalletNumber,
            InstaPayId = req.InstaPayId,
            CommercialName = req.CommercialName,
            CommercialAddress = req.CommercialAddress,
            NationalIdNumber = req.NationalIdNumber,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        return Results.Created($"/api/v1/customers/{customer.Id}",
            new CustomerDto(customer.Id, customer.Code, customer.CustomerCode, customer.Name,
                customer.Phone, customer.Phone2, customer.Notes,
                customer.WhatsApp, customer.WalletNumber, customer.InstaPayId,
                customer.CommercialName, customer.CommercialAddress, customer.NationalIdNumber,
                customer.IsActive, customer.CreatedAtUtc));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, [FromBody] UpdateCustomerRequest req, AppDbContext db)
    {
        var customer = await db.Customers.FindAsync(id);
        if (customer is null) return Problem(404, ErrorCodes.NotFound, "Customer not found.");

        if (req.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Problem(400, ErrorCodes.ValidationFailed, "Name cannot be empty.");
            customer.Name = req.Name;
        }

        if (req.Code is not null)
        {
            // CAFS-1-R1: Code is immutable after creation — ignore attempts to change it
        }

        if (req.Phone is not null) customer.Phone = req.Phone;
        if (req.Phone2 is not null) customer.Phone2 = req.Phone2;
        if (req.Notes is not null) customer.Notes = req.Notes;
        if (req.WhatsApp is not null) customer.WhatsApp = req.WhatsApp;
        if (req.WalletNumber is not null) customer.WalletNumber = req.WalletNumber;
        if (req.InstaPayId is not null) customer.InstaPayId = req.InstaPayId;
        if (req.CommercialName is not null) customer.CommercialName = req.CommercialName;
        if (req.CommercialAddress is not null) customer.CommercialAddress = req.CommercialAddress;
        if (req.NationalIdNumber is not null) customer.NationalIdNumber = req.NationalIdNumber;
        if (req.IsActive.HasValue) customer.IsActive = req.IsActive.Value;

        customer.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(new { message = "Customer updated." });
    }

    private static async Task<IResult> DeleteAsync(Guid id, AppDbContext db)
    {
        var customer = await db.Customers.FindAsync(id);
        if (customer is null) return Problem(404, ErrorCodes.NotFound, "Customer not found.");

        customer.IsActive = false;
        customer.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(new { message = "Customer deactivated." });
    }

    private static IResult Problem(int status, string code, string detail)
        => Results.Problem(detail: detail, title: code, statusCode: status,
            type: $"https://elshazlystore.local/errors/{code.ToLowerInvariant()}");

    // ═══ Attachment endpoints ═══

    private static async Task<IResult> ListAttachmentsAsync(Guid id, AppDbContext db)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == id))
            return Problem(404, ErrorCodes.NotFound, "Customer not found.");

        var attachments = await db.CustomerAttachments
            .Where(a => a.CustomerId == id)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new CustomerAttachmentDto(a.Id, a.CustomerId, a.FileName, a.ContentType, a.FileSize, a.Category, a.CreatedAtUtc))
            .ToListAsync();

        return Results.Ok(attachments);
    }

    private static async Task<IResult> UploadAttachmentAsync(
        Guid id, HttpRequest request, AppDbContext db,
        CustomerAttachmentStorageService storageService,
        [FromQuery] string? category)
    {
        var customer = await db.Customers.FindAsync(id);
        if (customer is null)
            return Problem(404, ErrorCodes.NotFound, "Customer not found.");

        if (string.IsNullOrWhiteSpace(customer.CustomerCode))
            return Problem(500, ErrorCodes.ValidationFailed,
                "Customer does not have a global CustomerCode. Cannot store attachment.");

        if (!request.HasFormContentType || request.Form.Files.Count == 0)
            return Problem(400, ErrorCodes.ValidationFailed, "A file must be uploaded as multipart/form-data.");

        var file = request.Form.Files[0];
        if (file.Length == 0)
            return Problem(400, ErrorCodes.ValidationFailed, "File is empty.");
        if (file.Length > MaxAttachmentSize)
            return Problem(400, ErrorCodes.ValidationFailed, "File size exceeds the 5 MB limit.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return Problem(400, ErrorCodes.ValidationFailed, $"File type '{ext}' is not allowed.");

        var cat = string.IsNullOrWhiteSpace(category) ? "other" : category.Trim().ToLowerInvariant();
        var validCategories = new[] { "national_id", "contract", "other" };
        if (!validCategories.Contains(cat))
            cat = "other";

        // Save file to filesystem
        var originalFileName = Path.GetFileName(file.FileName);
        using var stream = file.OpenReadStream();
        var (storedFileName, relativePath) = await storageService.SaveFileAsync(
            customer.CustomerCode, originalFileName, stream);

        var attachment = new CustomerAttachment
        {
            Id = Guid.NewGuid(),
            CustomerId = id,
            FileName = originalFileName,
            StoredFileName = storedFileName,
            RelativePath = relativePath,
            CustomerCode = customer.CustomerCode,
            ContentType = file.ContentType ?? "application/octet-stream",
            FileSize = file.Length,
            Category = cat,
            FileContent = null, // new attachments: no blob, filesystem only
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.CustomerAttachments.Add(attachment);
        await db.SaveChangesAsync();

        return Results.Created($"/api/v1/customers/{id}/attachments/{attachment.Id}",
            new CustomerAttachmentDto(attachment.Id, attachment.CustomerId, attachment.FileName,
                attachment.ContentType, attachment.FileSize, attachment.Category, attachment.CreatedAtUtc));
    }

    private static async Task<IResult> DownloadAttachmentAsync(
        Guid id, Guid attachmentId, AppDbContext db,
        CustomerAttachmentStorageService storageService)
    {
        var attachment = await db.CustomerAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.CustomerId == id);

        if (attachment is null)
            return Problem(404, ErrorCodes.NotFound, "Attachment not found.");

        // New filesystem-stored attachment: read from disk
        if (!string.IsNullOrEmpty(attachment.RelativePath))
        {
            var fileBytes = storageService.ReadFile(attachment.RelativePath);
            if (fileBytes is null)
                return Problem(404, ErrorCodes.NotFound, "Attachment file not found on disk.");

            return Results.File(fileBytes, attachment.ContentType, attachment.FileName);
        }

        // Legacy blob-stored attachment: read from database
        if (attachment.FileContent is not null && attachment.FileContent.Length > 0)
        {
            return Results.File(attachment.FileContent, attachment.ContentType, attachment.FileName);
        }

        return Problem(404, ErrorCodes.NotFound, "Attachment content not available.");
    }

    private static async Task<IResult> DeleteAttachmentAsync(
        Guid id, Guid attachmentId, AppDbContext db,
        CustomerAttachmentStorageService storageService)
    {
        var attachment = await db.CustomerAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.CustomerId == id);

        if (attachment is null)
            return Problem(404, ErrorCodes.NotFound, "Attachment not found.");

        // Delete filesystem file if it exists
        if (!string.IsNullOrEmpty(attachment.RelativePath))
        {
            storageService.DeleteFile(attachment.RelativePath);
        }

        db.CustomerAttachments.Remove(attachment);
        await db.SaveChangesAsync();

        return Results.Ok(new { message = "Attachment deleted." });
    }

    /// <summary>
    /// Generates a unique immutable CustomerCode in YYYY-NNNNNN format.
    /// </summary>
    private static async Task<string> GenerateCustomerCodeAsync(AppDbContext db)
    {
        var year = DateTime.UtcNow.Year.ToString();
        var prefix = $"{year}-";

        var maxCode = await db.Customers
            .Where(c => c.CustomerCode.StartsWith(prefix))
            .Select(c => c.CustomerCode)
            .OrderByDescending(c => c)
            .FirstOrDefaultAsync();

        var nextSeq = 1;
        if (maxCode is not null)
        {
            var seqPart = maxCode.Substring(prefix.Length);
            if (int.TryParse(seqPart, out var parsed))
                nextSeq = parsed + 1;
        }

        return $"{year}-{nextSeq:D6}";
    }

    /// <summary>
    /// CAFS-1-R1: Returns the full filesystem path for the customer's attachments folder.
    /// </summary>
    private static async Task<IResult> GetAttachmentsFolderAsync(
        Guid id, AppDbContext db, CustomerAttachmentStorageService storageService)
    {
        var customer = await db.Customers.FindAsync(id);
        if (customer is null)
            return Problem(404, ErrorCodes.NotFound, "Customer not found.");

        if (string.IsNullOrWhiteSpace(customer.CustomerCode))
            return Problem(500, ErrorCodes.ValidationFailed,
                "Customer does not have a global CustomerCode.");

        var folderPath = Path.Combine(storageService.RootPath, customer.CustomerCode);
        var exists = Directory.Exists(folderPath);

        return Results.Ok(new AttachmentsFolderResponse(folderPath, exists));
    }

    // DTOs
    public sealed record CustomerDto(Guid Id, string Code, string CustomerCode, string Name, string? Phone,
        string? Phone2, string? Notes,
        string? WhatsApp, string? WalletNumber, string? InstaPayId,
        string? CommercialName, string? CommercialAddress, string? NationalIdNumber,
        bool IsActive, DateTime CreatedAtUtc);
    public sealed record CreateCustomerRequest(string Name, string? Code, string? Phone,
        string? Phone2, string? Notes,
        string? WhatsApp, string? WalletNumber, string? InstaPayId,
        string? CommercialName, string? CommercialAddress, string? NationalIdNumber);
    public sealed record UpdateCustomerRequest(string? Name, string? Code, string? Phone,
        string? Phone2, string? Notes,
        string? WhatsApp, string? WalletNumber, string? InstaPayId,
        string? CommercialName, string? CommercialAddress, string? NationalIdNumber,
        bool? IsActive);
    public sealed record CustomerAttachmentDto(Guid Id, Guid CustomerId, string FileName,
        string ContentType, long FileSize, string Category, DateTime CreatedAtUtc);
    public sealed record AttachmentsFolderResponse(string FolderPath, bool Exists);
}
