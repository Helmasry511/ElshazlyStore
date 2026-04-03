using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ElshazlyStore.Infrastructure.Services;

/// <summary>
/// Manages filesystem storage for customer attachments.
/// Files are stored under: {RootPath}/{CustomerCode}/{StoredFileName}
/// </summary>
public sealed class CustomerAttachmentStorageService
{
    private readonly string _rootPath;
    private readonly ILogger<CustomerAttachmentStorageService> _logger;

    public CustomerAttachmentStorageService(
        IConfiguration configuration,
        ILogger<CustomerAttachmentStorageService> logger)
    {
        _logger = logger;

        // Resolve root path: configured value or default to <ContentRoot>/CustomerAttachments
        var configured = configuration["AttachmentStorage:RootPath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            _rootPath = Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(AppContext.BaseDirectory, configured);
        }
        else
        {
            _rootPath = Path.Combine(AppContext.BaseDirectory, "CustomerAttachments");
        }

        _logger.LogInformation("Customer attachment storage root: {RootPath}", _rootPath);
    }

    /// <summary>
    /// Gets the resolved root path for customer attachments.
    /// </summary>
    public string RootPath => _rootPath;

    /// <summary>
    /// Ensures the customer folder exists and returns its full path.
    /// </summary>
    public string EnsureCustomerFolder(string customerCode)
    {
        ValidateCustomerCode(customerCode);
        var folderPath = Path.Combine(_rootPath, customerCode);
        Directory.CreateDirectory(folderPath);
        return folderPath;
    }

    /// <summary>
    /// Generates a collision-safe stored file name preserving the original extension.
    /// Format: {timestamp}_{shortGuid}{extension}
    /// </summary>
    public static string GenerateStoredFileName(string originalFileName)
    {
        var ext = Path.GetExtension(originalFileName)?.ToLowerInvariant() ?? string.Empty;
        // Sanitize extension
        ext = SanitizeExtension(ext);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        return $"{timestamp}_{uniqueSuffix}{ext}";
    }

    /// <summary>
    /// Saves file bytes to the customer's folder on disk.
    /// Returns (storedFileName, relativePath).
    /// </summary>
    public async Task<(string StoredFileName, string RelativePath)> SaveFileAsync(
        string customerCode, string originalFileName, Stream fileStream)
    {
        var folderPath = EnsureCustomerFolder(customerCode);
        var storedFileName = GenerateStoredFileName(originalFileName);
        var fullPath = Path.Combine(folderPath, storedFileName);
        var relativePath = Path.Combine(customerCode, storedFileName);

        await using var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write);
        await fileStream.CopyToAsync(fs);

        _logger.LogInformation(
            "Saved customer attachment: {RelativePath} ({OriginalName})",
            relativePath, originalFileName);

        return (storedFileName, relativePath);
    }

    /// <summary>
    /// Reads a file from the customer attachments filesystem.
    /// Returns null if the file does not exist.
    /// </summary>
    public byte[]? ReadFile(string relativePath)
    {
        var fullPath = Path.Combine(_rootPath, relativePath);
        if (!File.Exists(fullPath))
            return null;

        return File.ReadAllBytes(fullPath);
    }

    /// <summary>
    /// Deletes a file from the customer attachments filesystem.
    /// Returns true if the file was deleted, false if it didn't exist.
    /// </summary>
    public bool DeleteFile(string relativePath)
    {
        var fullPath = Path.Combine(_rootPath, relativePath);
        if (!File.Exists(fullPath))
            return false;

        File.Delete(fullPath);
        _logger.LogInformation("Deleted customer attachment file: {RelativePath}", relativePath);
        return true;
    }

    /// <summary>
    /// Validates that a customer code is safe for filesystem folder naming.
    /// Must contain only digits and hyphens.
    /// </summary>
    private static void ValidateCustomerCode(string customerCode)
    {
        if (string.IsNullOrWhiteSpace(customerCode))
            throw new ArgumentException("CustomerCode cannot be empty.", nameof(customerCode));

        foreach (var c in customerCode)
        {
            if (!char.IsDigit(c) && c != '-')
                throw new ArgumentException(
                    $"CustomerCode contains invalid character '{c}'. Only digits and '-' are allowed.",
                    nameof(customerCode));
        }
    }

    /// <summary>
    /// Sanitizes a file extension to remove invalid path characters.
    /// </summary>
    private static string SanitizeExtension(string ext)
    {
        if (string.IsNullOrEmpty(ext)) return string.Empty;
        // Only keep alphanumeric + leading dot
        var sanitized = new char[ext.Length];
        var idx = 0;
        foreach (var c in ext)
        {
            if (char.IsLetterOrDigit(c) || c == '.')
                sanitized[idx++] = c;
        }
        return new string(sanitized, 0, idx);
    }
}
