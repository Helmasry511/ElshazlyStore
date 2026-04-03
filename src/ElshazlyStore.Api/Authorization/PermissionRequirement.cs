using Microsoft.AspNetCore.Authorization;

namespace ElshazlyStore.Api.Authorization;

/// <summary>
/// Authorization requirement: the user must have a specific permission code claim.
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionCode { get; }

    public PermissionRequirement(string permissionCode)
    {
        PermissionCode = permissionCode;
    }
}
