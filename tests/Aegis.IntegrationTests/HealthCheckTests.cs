using System.Net;
using Aegis.IntegrationTests.Fixtures;

namespace Aegis.IntegrationTests;

[Collection("Integration")]
public class HealthCheckTests
{
    private readonly AegisApiFactory _factory;
    private readonly HttpClient _client;

    public HealthCheckTests(AegisApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RootEndpoint_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
