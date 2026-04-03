namespace ElshazlyStore.Desktop.Services;

/// <summary>
/// Checks whether the current user has specific permissions.
/// Centralizes permission logic for the entire UI layer.
/// </summary>
public interface IPermissionService
{
    /// <summary>Returns true if the current user has the given permission code.</summary>
    bool HasPermission(string permissionCode);

    /// <summary>Returns true if the current user has ALL of the given permission codes.</summary>
    bool HasAllPermissions(params string[] permissionCodes);

    /// <summary>Returns true if the current user has ANY of the given permission codes.</summary>
    bool HasAnyPermission(params string[] permissionCodes);
}
