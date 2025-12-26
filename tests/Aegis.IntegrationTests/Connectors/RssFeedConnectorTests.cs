using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using Aegis.Infrastructure.Persistence;
using Aegis.Infrastructure.Persistence.Repositories;
using Aegis.Infrastructure.Services.Connectors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Testcontainers.PostgreSql;

namespace Aegis.IntegrationTests.Connectors;

public class RssFeedConnectorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _aegisDatabase = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .WithDatabase("aegis_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private WebApplication? _testServer;
    private string _testServerUrl = string.Empty;
    private const int TestPort = 5555;
    private IDocumentRepository _documentRepository = null!;
    private IDataConnector _connector = null!;

    public async Task InitializeAsync()
    {
        await _aegisDatabase.StartAsync();

        // Run AEGIS migrations
        var migrator = new DatabaseMigrator(_aegisDatabase.GetConnectionString());
        migrator.Migrate();

        // Create repository and connector
        _documentRepository = new DocumentRepository(_aegisDatabase.GetConnectionString());
        var logger = Substitute.For<ILogger<RssFeedConnector>>();
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(new HttpClient());
        _connector = new RssFeedConnector(_documentRepository, logger, httpClientFactory);

        // Create test HTTP server for RSS feeds
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{TestPort}");
        _testServer = builder.Build();

        _testServer.MapGet("/rss", async context =>
        {
            var feed = CreateTestFeed();
            var xmlWriter = XmlWriter.Create(context.Response.Body, new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Async = true
            });

            context.Response.ContentType = "application/rss+xml";
            var formatter = new Rss20FeedFormatter(feed);
            formatter.WriteTo(xmlWriter);
            await xmlWriter.FlushAsync();
        });

        _testServer.MapGet("/empty", async context =>
        {
            var feed = new SyndicationFeed("Empty Feed", "No items", new Uri("http://test.com"));
            var xmlWriter = XmlWriter.Create(context.Response.Body, new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Async = true
            });

            context.Response.ContentType = "application/rss+xml";
            var formatter = new Rss20FeedFormatter(feed);
            formatter.WriteTo(xmlWriter);
            await xmlWriter.FlushAsync();
        });

        await _testServer.StartAsync();
        _testServerUrl = $"http://localhost:{TestPort}";
    }

    public async Task DisposeAsync()
    {
        if (_testServer != null)
        {
            await _testServer.StopAsync();
            await _testServer.DisposeAsync();
        }
        await _aegisDatabase.DisposeAsync();
    }

    private static SyndicationFeed CreateTestFeed()
    {
        var feed = new SyndicationFeed(
            "Test RSS Feed",
            "A test feed for unit testing",
            new Uri("http://test.com"),
            "test-feed-id",
            DateTime.UtcNow);

        var items = new List<SyndicationItem>
        {
            new SyndicationItem(
                "Article 1",
                "Summary of article 1",
                new Uri("http://test.com/article1"),
                "article-1",
                DateTime.UtcNow.AddDays(-2))
            {
                Content = SyndicationContent.CreatePlaintextContent("Full content of article 1")
            },
            new SyndicationItem(
                "Article 2",
                "Summary of article 2",
                new Uri("http://test.com/article2"),
                "article-2",
                DateTime.UtcNow.AddDays(-1))
            {
                Content = SyndicationContent.CreatePlaintextContent("Full content of article 2")
            },
            new SyndicationItem(
                "Article 3",
                "Summary of article 3",
                new Uri("http://test.com/article3"),
                "article-3",
                DateTime.UtcNow)
            {
                Content = SyndicationContent.CreatePlaintextContent("Full content of article 3")
            }
        };

        feed.Items = items;
        return feed;
    }

    [Fact]
    public async Task ValidateSettingsAsync_WithValidSettings_ShouldSucceed()
    {
        // Arrange
        var settings = new Dictionary<string, string>
        {
            ["feed_url"] = $"{_testServerUrl}/rss"
        };

        // Act
        var result = await _connector.ValidateSettingsAsync(settings);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateSettingsAsync_WithMissingSettings_ShouldFail()
    {
        // Arrange
        var settings = new Dictionary<string, string>();

        // Act
        var result = await _connector.ValidateSettingsAsync(settings);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_WithValidRssFeed_ShouldSucceed()
    {
        // Arrange
        var settings = new Dictionary<string, string>
        {
            ["feed_url"] = $"{_testServerUrl}/rss"
        };

        // Act
        var result = await _connector.TestConnectionAsync(settings);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidUrl_ShouldFail()
    {
        // Arrange
        var settings = new Dictionary<string, string>
        {
            ["feed_url"] = "http://localhost:9999/nonexistent"
        };

        // Act
        var result = await _connector.TestConnectionAsync(settings);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SyncAsync_WithRssFeed_ShouldFetchAllArticles()
    {
        // Arrange
        var dataSourceId = Guid.NewGuid();
        var settings = new Dictionary<string, string>
        {
            ["feed_url"] = $"{_testServerUrl}/rss"
        };

        var syncOptions = new SyncOptions(IsFullSync: true, LastSyncCursor: null);

        // Act
        var result = await _connector.SyncAsync(dataSourceId, settings, syncOptions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DocumentsAdded.Should().Be(3);
        result.Value.DocumentsFailed.Should().Be(0);
    }

    [Fact]
    public async Task SyncAsync_WithEmptyFeed_ShouldReturnNoDocuments()
    {
        // Arrange
        var dataSourceId = Guid.NewGuid();
        var settings = new Dictionary<string, string>
        {
            ["feed_url"] = $"{_testServerUrl}/empty"
        };

        var syncOptions = new SyncOptions(IsFullSync: true, LastSyncCursor: null);

        // Act
        var result = await _connector.SyncAsync(dataSourceId, settings, syncOptions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.DocumentsAdded.Should().Be(0);
    }
}
