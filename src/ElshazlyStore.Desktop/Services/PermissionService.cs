namespace ElshazlyStore.Desktop.Services;

public sealed class PermissionService : IPermissionService
{
    private readonly ISessionService _sessionService;

    public PermissionService(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

    public bool HasPermission(string permissionCode) =>
        _sessionService.Permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);

    public bool HasAllPermissions(params string[] permissionCodes) =>
        permissionCodes.All(code => HasPermission(code));

    public bool HasAnyPermission(params string[] permissionCodes) =>
        permissionCodes.Any(code => HasPermission(code));
}
