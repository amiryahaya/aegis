using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Aegis.IntegrationTests.Fixtures;
using FluentAssertions;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocxDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using DocxBody = DocumentFormat.OpenXml.Wordprocessing.Body;
using DocxParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using DocxRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using DocxText = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace Aegis.IntegrationTests.Endpoints;

[Collection("Database")]
public class DocumentEndpointsTests : IAsyncLifetime
{
    private readonly AegisApiFactory _factory;
    private readonly HttpClient _client;
    private Guid _userId;
    private Guid _workspaceId;
    private Guid _teamId;
    private Guid _dataSourceId;
    private string _token = string.Empty;

    public DocumentEndpointsTests(AegisApiFactory factory)
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
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UploadDocument_WithValidPdf_ShouldSucceed()
    {
        // Arrange
        var pdfContent = CreateTestPdf("Test PDF content for machine learning and AI");

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(pdfContent), "file", "test.pdf");
        form.Add(new StringContent(_dataSourceId.ToString()), "dataSourceId");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/teams/{_teamId}/documents/upload")
        {
            Content = form,
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", _token) }
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("documentId").GetGuid().Should().NotBeEmpty();
        json.RootElement.GetProperty("fileName").GetString().Should().Be("test.pdf");
        json.RootElement.GetProperty("chunkCount").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UploadDocument_WithValidDocx_ShouldSucceed()
    {
        // Arrange
        var docxContent = CreateTestDocx("Test DOCX content about quantum computing and algorithms");

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(docxContent), "file", "test.docx");
        form.Add(new StringContent(_dataSourceId.ToString()), "dataSourceId");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/teams/{_teamId}/documents/upload")
        {
            Content = form,
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", _token) }
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("documentId").GetGuid().Should().NotBeEmpty();
        json.RootElement.GetProperty("fileName").GetString().Should().Be("test.docx");
        json.RootElement.GetProperty("chunkCount").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UploadDocument_WithUnsupportedFileType_ShouldReturnBadRequest()
    {
        // Arrange
        var content = new byte[] { 1, 2, 3, 4 };

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(content), "file", "test.txt");
        form.Add(new StringContent(_dataSourceId.ToString()), "dataSourceId");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/teams/{_teamId}/documents/upload")
        {
            Content = form,
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", _token) }
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadDocument_WithNonExistentDataSource_ShouldReturnNotFound()
    {
        // Arrange
        var pdfContent = CreateTestPdf("Test content");
        var nonExistentDataSourceId = Guid.NewGuid();

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(pdfContent), "file", "test.pdf");
        form.Add(new StringContent(nonExistentDataSourceId.ToString()), "dataSourceId");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/teams/{_teamId}/documents/upload")
        {
            Content = form,
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", _token) }
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UploadDocument_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        var pdfContent = CreateTestPdf("Test content");

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(pdfContent), "file", "test.pdf");
        form.Add(new StringContent(_dataSourceId.ToString()), "dataSourceId");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/teams/{_teamId}/documents/upload")
        {
            Content = form
            // No authorization header
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    private static byte[] CreateTestDocx(string content)
    {
        using var memoryStream = new MemoryStream();
        using var wordDocument = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document);

        var mainPart = wordDocument.AddMainDocumentPart();
        mainPart.Document = new DocxDocument();
        var body = mainPart.Document.AppendChild(new DocxBody());
        var paragraph = body.AppendChild(new DocxParagraph());
        var run = paragraph.AppendChild(new DocxRun());
        run.AppendChild(new DocxText(content));

        mainPart.Document.Save();
        wordDocument.Dispose();

        return memoryStream.ToArray();
    }
}
