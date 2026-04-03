using System.Net;

namespace ElshazlyStore.Tests.Api;

/// <summary>
/// Integration tests for the health endpoint.
/// </summary>
[Collection("Integration")]
public sealed class HealthEndpointTests
{
    private readonly HttpClient _client;

    public HealthEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_ReturnsCorrelationIdHeader()
    {
        var response = await _client.GetAsync("/api/v1/health");
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
    }

    [Fact]
    public async Task Health_ForwardsCorrelationId()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/health");
        var expected = Guid.NewGuid().ToString();
        request.Headers.Add("X-Correlation-Id", expected);

        var response = await _client.SendAsync(request);

        Assert.Equal(expected, response.Headers.GetValues("X-Correlation-Id").First());
    }
}
