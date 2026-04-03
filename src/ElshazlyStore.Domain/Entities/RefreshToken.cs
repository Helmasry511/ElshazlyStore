namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Opaque refresh token stored in DB. Only the HMAC-SHA256 hash of the
/// raw token is persisted — the plaintext is returned to the client once.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    /// <summary>HMAC-SHA256 hash of the raw refresh token (Base64).</summary>
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAtUtc { get; set; }
    /// <summary>Hash of the replacement token (rotation chain).</summary>
    public string? ReplacedByTokenHash { get; set; }

    public User User { get; set; } = null!;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsRevoked => RevokedAtUtc is not null;
    public bool IsActive => !IsExpired && !IsRevoked;
}
