namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Health-check endpoint: GET /api/v1/health
/// </summary>
public static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/health", () => Results.Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow
        }))
        .WithName("HealthCheck")
        .WithTags("Health")
        .Produces(200);

        return group;
    }
}
