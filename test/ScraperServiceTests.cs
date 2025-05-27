using copilotTest.Models;
using copilotTest.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace copilotTest.Tests
{
    /// <summary>
    /// Tests for Scraper Service functionality
    /// </summary>
    public class ScraperServiceTests
    {
        private readonly Mock<IDataService> _mockDataService;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<ScraperService>> _mockLogger;
        private readonly ScraperService _scraperService;

        public ScraperServiceTests()
        {
            _mockDataService = new Mock<IDataService>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLogger = new Mock<ILogger<ScraperService>>();

            _scraperService = new ScraperService(
                _mockDataService.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object);
        }

        /// <summary>
        /// Test that ScrapeUrlsAsync can handle empty URL list
        /// </summary>
        [Fact]
        public async Task ScrapeUrlsAsync_WithEmptyUrls_ShouldReturnEmptyList()
        {
            // Arrange
            var batchRequest = new BatchScrapingRequestDto
            {
                Urls = new List<string>()
            };

            // Act
            var result = await _scraperService.ScrapeUrlsAsync(batchRequest);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Test that ScrapeUrlsAsync filters out invalid URLs
        /// </summary>
        [Fact]
        public async Task ScrapeUrlsAsync_WithInvalidUrls_ShouldFilterThem()
        {
            // Arrange
            var batchRequest = new BatchScrapingRequestDto
            {
                Urls = new List<string> { "", null, "   " }
            };

            // Act
            var result = await _scraperService.ScrapeUrlsAsync(batchRequest);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Verify that DataController processes both single URLs and URLs array correctly
        /// This is an integration test for the improved unified endpoint
        /// </summary>
        [Fact(Skip = "Integration test - requires actual HTTP calls")]
        public async Task ScrapeUrlsAsync_IntegrationTest()
        {
            // This test is marked as skipped because it requires actual HTTP calls
            // but it can be used for manual integration testing

            // Test with single URL
            var singleRequest = new BatchScrapingRequestDto
            {
                Urls = new List<string> { "https://example.com" }
            };
            
            var singleResult = await _scraperService.ScrapeUrlsAsync(singleRequest);
            Assert.Single(singleResult);
            Assert.Equal("https://example.com", singleResult[0].Url);
            
            // Test with multiple URLs
            var multipleRequest = new BatchScrapingRequestDto
            {
                Urls = new List<string> { "https://example.com", "https://example.org" }
            };
            
            var multipleResult = await _scraperService.ScrapeUrlsAsync(multipleRequest);
            Assert.Equal(2, multipleResult.Count);
        }
    }
}