using copilotTest.Infrastructure;
using copilotTest.Models;
using copilotTest.Services;
using LiteDB;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace copilotTest.Tests
{
    /// <summary>
    /// Tests for Data Service functionality
    /// </summary>
    public class DataServiceTests : IDisposable
    {
        private readonly LiteDatabase _database;
        private readonly ILiteDbContext _dbContext;
        private readonly IDataService _dataService;
        private readonly ILogger<DataService> _logger;

        /// <summary>
        /// Set up test environment with in-memory database
        /// </summary>
        public DataServiceTests()
        {
            // Create in-memory LiteDB database for testing
            _database = new LiteDatabase(":memory:");
            _dbContext = new LiteDbContext(_database);
            
            // Create mock logger
            _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<DataService>();
            
            // Create data service with test dependencies
            _dataService = new DataService(_dbContext, _logger);
        }

        /// <summary>
        /// Test adding and retrieving a scraped data item
        /// </summary>
        [Fact]
        public void SaveAndGetById_ShouldReturnSavedItem()
        {
            // Arrange
            var testData = new ScrapedData
            {
                Url = "https://example.com",
                Title = "Test Page",
                Content = "<html><body>Test content</body></html>",
                ScrapedDate = DateTime.UtcNow,
                ContentType = "text/html"
            };

            // Act
            var savedData = _dataService.SaveData(testData);
            var retrievedData = _dataService.GetById(savedData.Id);

            // Assert
            Assert.NotNull(retrievedData);
            Assert.Equal(testData.Url, retrievedData.Url);
            Assert.Equal(testData.Title, retrievedData.Title);
            Assert.Equal(testData.Content, retrievedData.Content);
        }

        /// <summary>
        /// Test deduplication by content hash
        /// </summary>
        [Fact]
        public void SaveDuplicateData_ShouldReturnExistingItem()
        {
            // Arrange
            var testData1 = new ScrapedData
            {
                Url = "https://example.com",
                Title = "Test Page",
                Content = "<html><body>Test content</body></html>",
                ScrapedDate = DateTime.UtcNow,
                ContentType = "text/html"
            };

            var testData2 = new ScrapedData
            {
                Url = "https://example.com/duplicate",  // Different URL
                Title = "Test Page",                    // Same title
                Content = "<html><body>Test content</body></html>", // Same content
                ScrapedDate = DateTime.UtcNow.AddHours(1),         // Different time
                ContentType = "text/html"
            };

            // Act
            var savedData1 = _dataService.SaveData(testData1);
            var savedData2 = _dataService.SaveData(testData2);

            // Assert - should return same ID due to content-based deduplication
            Assert.Equal(savedData1.Id, savedData2.Id);
            Assert.Equal(savedData1.ContentHash, savedData2.ContentHash);
        }

        /// <summary>
        /// Test deletion of data
        /// </summary>
        [Fact]
        public void DeleteData_ShouldRemoveItem()
        {
            // Arrange
            var testData = new ScrapedData
            {
                Url = "https://example.com/delete-test",
                Title = "Delete Test",
                Content = "<html><body>Content to be deleted</body></html>",
                ScrapedDate = DateTime.UtcNow
            };

            // Act
            var savedData = _dataService.SaveData(testData);
            var deleteResult = _dataService.DeleteData(savedData.Id);
            var retrievedData = _dataService.GetById(savedData.Id);

            // Assert
            Assert.True(deleteResult);
            Assert.Null(retrievedData);
        }

        /// <summary>
        /// Test pagination and filtering
        /// </summary>
        [Fact]
        public void GetAll_WithFiltering_ShouldReturnMatchingItems()
        {
            // Arrange - Create multiple test items
            for (int i = 0; i < 5; i++)
            {
                _dataService.SaveData(new ScrapedData
                {
                    Url = $"https://example.com/page-{i}",
                    Title = $"Test Page {i}",
                    Content = $"<html><body>Content for page {i}</body></html>",
                    ScrapedDate = DateTime.UtcNow.AddDays(-i)
                });
            }

            // Add a different domain item
            _dataService.SaveData(new ScrapedData
            {
                Url = "https://different.com/page",
                Title = "Different Domain",
                Content = "<html><body>Different domain content</body></html>",
                ScrapedDate = DateTime.UtcNow
            });

            // Act - Filter by URL
            var (exampleItems, exampleCount) = _dataService.GetAll(urlFilter: "example.com");
            var (differentItems, differentCount) = _dataService.GetAll(urlFilter: "different.com");

            // Assert
            Assert.Equal(5, exampleCount);
            Assert.Equal(5, exampleItems.Count);
            Assert.Equal(1, differentCount);
            Assert.Single(differentItems);
        }

        /// <summary>
        /// Test data export functionality
        /// </summary>
        [Fact]
        public void ExportData_ShouldGenerateValidOutput()
        {
            // Arrange - Create some test data
            for (int i = 0; i < 3; i++)
            {
                _dataService.SaveData(new ScrapedData
                {
                    Url = $"https://export-test.com/page-{i}",
                    Title = $"Export Test Page {i}",
                    Content = $"<html><body>Export content {i}</body></html>",
                    ScrapedDate = DateTime.UtcNow.AddDays(-i)
                });
            }

            // Act - Export as JSON
            var jsonExport = _dataService.ExportData(new ExportRequestDto { Format = "json" });

            // Read the exported data
            using var reader = new StreamReader(jsonExport);
            var jsonContent = reader.ReadToEnd();
            
            // Parse the JSON
            var exportedItems = JsonSerializer.Deserialize<ScrapedDataDto[]>(jsonContent);

            // Assert
            Assert.NotNull(exportedItems);
            Assert.Equal(3, exportedItems?.Length);
        }

        /// <summary>
        /// Test GetAll with invalid pagination parameters
        /// </summary>
        [Fact]
        public void GetAll_WithInvalidParameters_ShouldHandleGracefully()
        {
            // Arrange - Add some test data
            for (int i = 0; i < 3; i++)
            {
                _dataService.SaveData(new ScrapedData
                {
                    Url = $"https://example.com/page-{i}",
                    Title = $"Test Page {i}",
                    Content = $"<html><body>Content {i}</body></html>",
                    ScrapedDate = DateTime.UtcNow
                });
            }

            // Act - Test with invalid parameters
            var (negPageResults, negPageCount) = _dataService.GetAll(page: -1, pageSize: 10);
            var (negSizeResults, negSizeCount) = _dataService.GetAll(page: 1, pageSize: -5);
            var (zeroSizeResults, zeroSizeCount) = _dataService.GetAll(page: 1, pageSize: 0);

            // Assert - Should handle gracefully and return reasonable results
            Assert.NotNull(negPageResults);
            Assert.NotNull(negSizeResults);
            Assert.NotNull(zeroSizeResults);
            
            // Total count should still be accurate
            Assert.Equal(3, negPageCount);
            Assert.Equal(3, negSizeCount);
            Assert.Equal(3, zeroSizeCount);
        }

        /// <summary>
        /// Test export data with invalid format
        /// </summary>
        [Fact]
        public void ExportData_WithInvalidFormat_ShouldNotThrowException()
        {
            // Arrange
            var exportRequest = new ExportRequestDto
            {
                Format = "invalid_format"
            };

            // Act & Assert - Should not throw exception
            var stream = _dataService.ExportData(exportRequest);
            
            // Additional validation
            Assert.NotNull(stream);
            Assert.True(stream.Length > 0);
        }

        /// <summary>
        /// Test saving data with invalid content
        /// </summary>
        [Fact]
        public void SaveData_WithInvalidContent_ShouldHandleGracefully()
        {
            // Arrange
            var invalidData = new ScrapedData
            {
                // URL is mandatory for content hash generation
                Url = "https://example.com/invalid",
                // Empty content and title which will cause issues with content hash
                Content = string.Empty,
                Title = string.Empty
            };

            // Act - This should not throw an exception
            var result = _dataService.SaveData(invalidData);

            // Assert - Original data should be returned
            Assert.Equal(invalidData.Id, result.Id);
            Assert.Equal(invalidData.Url, result.Url);
        }

        /// <summary>
        /// Test deleting data with non-existent ID
        /// </summary>
        [Fact]
        public void DeleteData_WithNonExistentId_ShouldReturnFalse()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();

            // Act
            var result = _dataService.DeleteData(nonExistentId);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Test retrieving a non-existent ID
        /// </summary>
        [Fact]
        public void GetById_WithNonExistentId_ShouldReturnNull()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();

            // Act
            var result = _dataService.GetById(nonExistentId);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Clean up test environment
        /// </summary>
        public void Dispose()
        {
            _database?.Dispose();
        }
    }
}