namespace ElshazlyStore.Desktop.Models.Dtos;

public sealed class CustomerDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string CustomerCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Phone2 { get; set; }
    public string? Notes { get; set; }

    // CP-3A: Credit profile fields
    public string? WhatsApp { get; set; }
    public string? WalletNumber { get; set; }
    public string? InstaPayId { get; set; }
    public string? CommercialName { get; set; }
    public string? CommercialAddress { get; set; }
    public string? NationalIdNumber { get; set; }

    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class CreateCustomerRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Phone { get; set; }
    public string? Phone2 { get; set; }
    public string? Notes { get; set; }

    // CP-3A: Credit profile fields
    public string? WhatsApp { get; set; }
    public string? WalletNumber { get; set; }
    public string? InstaPayId { get; set; }
    public string? CommercialName { get; set; }
    public string? CommercialAddress { get; set; }
    public string? NationalIdNumber { get; set; }
}

public sealed class UpdateCustomerRequest
{
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Phone { get; set; }
    public string? Phone2 { get; set; }
    public string? Notes { get; set; }

    // CP-3A: Credit profile fields
    public string? WhatsApp { get; set; }
    public string? WalletNumber { get; set; }
    public string? InstaPayId { get; set; }
    public string? CommercialName { get; set; }
    public string? CommercialAddress { get; set; }
    public string? NationalIdNumber { get; set; }

    public bool? IsActive { get; set; }
}

public sealed class CustomerAttachmentDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

// CAFS-1-R1: Attachments folder path response
public sealed class AttachmentsFolderResponse
{
    public string FolderPath { get; set; } = string.Empty;
    public bool Exists { get; set; }
}
