namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Permission code string. Unique per system.
/// Authorisation checks compare JWT permission claims against these codes.
/// </summary>
public sealed class Permission
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Navigation
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
