namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Many-to-many join: Role ↔ Permission.
/// </summary>
public sealed class RolePermission
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }

    public Role Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
