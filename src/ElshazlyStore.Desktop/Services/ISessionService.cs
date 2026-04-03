using ElshazlyStore.Desktop.Models.Auth;

namespace ElshazlyStore.Desktop.Services;

/// <summary>
/// Manages authentication session: login, logout, current user, permissions.
/// </summary>
public interface ISessionService
{
    /// <summary>Current authenticated user info. Null if not logged in.</summary>
    MeResponse? CurrentUser { get; }

    /// <summary>Whether a user is currently authenticated.</summary>
    bool IsAuthenticated { get; }

    /// <summary>List of permission codes for the current user (from JWT claims via /auth/me).</summary>
    IReadOnlyList<string> Permissions { get; }

    /// <summary>Authenticate with username/password. Returns error message or null on success.</summary>
    Task<string?> LoginAsync(string username, string password);

    /// <summary>Log out the current user, revoke refresh token, clear stored tokens.</summary>
    Task LogoutAsync();

    /// <summary>Load user info from /auth/me using stored token (silent re-auth on startup).</summary>
    Task<bool> TryRestoreSessionAsync();

    /// <summary>Raised when the session ends (logout or token expired).</summary>
    event Action? SessionEnded;

    /// <summary>Raised when the session starts (login or restored).</summary>
    event Action? SessionStarted;
}
