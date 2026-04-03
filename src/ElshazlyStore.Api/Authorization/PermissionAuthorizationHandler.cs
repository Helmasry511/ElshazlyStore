using Microsoft.AspNetCore.Authorization;

namespace ElshazlyStore.Api.Authorization;

/// <summary>
/// Checks that the authenticated user has the required permission claim.
/// Permission codes are embedded in the JWT as "permission" claims.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var permissions = context.User.FindAll("permission").Select(c => c.Value);

        if (permissions.Contains(requirement.PermissionCode))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
