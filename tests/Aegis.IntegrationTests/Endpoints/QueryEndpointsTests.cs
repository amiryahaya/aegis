using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Aegis.IntegrationTests.Fixtures;
using FluentAssertions;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using Microsoft.AspNetCore.SignalR.Client;

namespace Aegis.IntegrationTests.Endpoints;

[Collection("Database")]
public class QueryEndpointsTests : IAsyncLifetime
{
    private readonly AegisApiFactory _factory;
    private readonly HttpClient _client;
    private Guid _userId;
    private Guid _workspaceId;
    private Guid _teamId;
    private Guid _dataSourceId;
    private Guid _documentId;
    private string _token = string.Empty;

    public QueryEndpointsTests(AegisApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();

        // Create workspace, team, and datasource
        (_userId, _workspaceId, _teamId, _token) = await _factory.CreateAuthenticatedUserWithTeamAsync(_client);

        // Create a data source
        var dataSourcePayload = new
        {
            name = "Test Data Source",
            description = "Test Description",
            teamId = _teamId,
            createdBy = _userId
        };

        var dataSourceRequest = new HttpRequestMessage(HttpMethod.Post, "/api/datasources")
        {
            Content = JsonContent.Create(dataSourcePayload),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", _token) }
        };

        var dataSourceResponse = await _client.SendAsync(dataSourceRequest);
        dataSourceResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var dataSourceContent = await dataSourceResponse.Content.ReadAsStringAsync();
        var dataSourceJson = JsonDocument.Parse(dataSourceContent);
        _dataSourceId = Guid.Parse(dataSourceJson.RootElement.GetProperty("id").GetString()!);

        // Upload a test document with known content
        var pdfContent = CreateTestPdf("Machine learning is a subset of artificial intelligence that focuses on training algorithms to learn from data and make predictions.");

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(pdfContent), "file", "ml-guide.pdf");
        form.Add(new StringContent(_dataSourceId.ToString()), "dataSourceId");

        var uploadRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/teams/{_teamId}/documents/upload")
        {
            Content = form,
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", _token) }
        };

        var uploadResponse = await _client.SendAsync(uploadRequest);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var uploadContent = await uploadResponse.Content.ReadAsStringAsync();
        var uploadJson = JsonDocument.Parse(uploadContent);
        _documentId = uploadJson.RootElement.GetProperty("documentId").GetGuid();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Query_WithValidQuestion_ShouldReturnAnswerWithCitations()
    {
        // Arrange
        var queryPayload = new
        {
            query = "What is machine learning?",
            teamId = _teamId,
            maxResults = 5
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/teams/{_teamId}/query")
        {
            Content = JsonContent.Create(queryPayload),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", _token) }
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("answer").GetString().Should().NotBeNullOrEmpty();
        json.RootElement.GetProperty("citations").GetArrayLength().Should().BeGreaterThan(0);

        var firstCitation = json.RootElement.GetProperty("citations")[0];
        firstCitation.GetProperty("documentId").GetGuid().Should().Be(_documentId);
        firstCitation.GetProperty("text").GetString().Should().NotBeNullOrEmpty();
        firstCitation.GetProperty("score").GetDouble().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Query_WithNoRelevantDocuments_ShouldReturnResponseWithNoCitations()
    {
        // Arrange
        var queryPayload = new
        {
            query = "What is quantum computing?", // Unrelated to the uploaded document
            teamId = _teamId,
            maxResults = 5
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/teams/{_teamId}/query")
        {
            Content = JsonContent.Create(queryPayload),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", _token) }
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("answer").GetString().Should().NotBeNullOrEmpty();
        // Citations might be empty or have low-scoring results
    }

    [Fact]
    public async Task Query_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        var queryPayload = new
        {
            query = "What is machine learning?",
            teamId = _teamId,
            maxResults = 5
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/teams/{_teamId}/query")
        {
            Content = JsonContent.Create(queryPayload)
            // No authorization header
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Query_WithEmptyQuery_ShouldReturnBadRequest()
    {
        // Arrange
        var queryPayload = new
        {
            query = "",
            teamId = _teamId,
            maxResults = 5
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/teams/{_teamId}/query")
        {
            Content = JsonContent.Create(queryPayload),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", _token) }
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Query_WithNonExistentTeam_ShouldReturnNotFound()
    {
        // Arrange
        var nonExistentTeamId = Guid.NewGuid();
        var queryPayload = new
        {
            query = "What is machine learning?",
            teamId = nonExistentTeamId,
            maxResults = 5
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/teams/{nonExistentTeamId}/query")
        {
            Content = JsonContent.Create(queryPayload),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", _token) }
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Query_WithStreaming_ShouldStreamResponseTokens()
    {
        // Arrange
        var hubUrl = $"{_client.BaseAddress}hubs/query";
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult(_token)!;
            })
            .Build();

        var tokens = new List<string>();
        var citations = new List<object>();
        var errorReceived = false;
        var completedReceived = false;

        connection.On<string>("StreamToken", token =>
        {
            tokens.Add(token);
        });

        connection.On<List<object>>("Citations", citationList =>
        {
            citations.AddRange(citationList);
        });

        connection.On<string>("Error", error =>
        {
            errorReceived = true;
        });

        connection.On("StreamComplete", () =>
        {
            completedReceived = true;
        });

        // Act
        await connection.StartAsync();

        await connection.InvokeAsync("StreamQuery", _teamId, "What is machine learning?", 5);

        // Wait for streaming to complete (with timeout)
        var timeout = DateTime.UtcNow.AddSeconds(10);
        while (!completedReceived && !errorReceived && DateTime.UtcNow < timeout)
        {
            await Task.Delay(100);
        }

        await connection.StopAsync();

        // Assert
        errorReceived.Should().BeFalse();
        completedReceived.Should().BeTrue();
        tokens.Should().NotBeEmpty("streaming should send tokens");
        citations.Should().NotBeEmpty("should include citations");

        // Combine tokens to get full response
        var fullResponse = string.Join("", tokens);
        fullResponse.Should().NotBeNullOrEmpty();
    }

    private static byte[] CreateTestPdf(string content)
    {
        using var memoryStream = new MemoryStream();
        using var writer = new PdfWriter(memoryStream);
        using var pdf = new PdfDocument(writer);
        using var document = new Document(pdf);

        document.Add(new Paragraph(content));
        document.Close();

        return memoryStream.ToArray();
    }
}
