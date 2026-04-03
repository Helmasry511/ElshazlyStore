namespace ElshazlyStore.Domain.Interfaces;

/// <summary>
/// Provides the current authenticated user's context from the HTTP request.
/// Used by the audit interceptor and business logic.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Username { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    string? CorrelationId { get; }
}
