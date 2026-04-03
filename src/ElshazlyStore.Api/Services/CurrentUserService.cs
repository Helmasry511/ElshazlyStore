using System.Security.Claims;
using ElshazlyStore.Domain.Interfaces;

namespace ElshazlyStore.Api.Services;

/// <summary>
/// Extracts the current authenticated user's context from the HTTP request.
/// Used by the audit interceptor and wherever user context is needed.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private HttpContext? Context => _accessor.HttpContext;

    public Guid? UserId
    {
        get
        {
            var sub = Context?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public string? Username => Context?.User.FindFirstValue(ClaimTypes.Name);

    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => Context?.Request.Headers.UserAgent.FirstOrDefault();

    public string? CorrelationId => Context?.Items.TryGetValue("CorrelationId", out var cid) == true
        ? cid?.ToString()
        : null;
}
