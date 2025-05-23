using copilotTest.Infrastructure;
using copilotTest.Models;
using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace copilotTest.Services
{
    /// <summary>
    /// Service for handling scraped data operations
    /// </summary>
    public interface IDataService
    {
        /// <summary>
        /// Get scraped data by ID
        /// </summary>
        /// <param name="id">ID of the data</param>
        /// <returns>Scraped data or null if not found</returns>
        ScrapedData? GetById(Guid id);

        /// <summary>
        /// Get all scraped data with optional filtering and pagination
        /// </summary>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Items per page</param>
        /// <param name="urlFilter">Optional URL filter</param>
        /// <param name="startDate">Optional start date filter</param>
        /// <param name="endDate">Optional end date filter</param>
        /// <returns>Paged result of scraped data</returns>
        (List<ScrapedData> Items, int TotalCount) GetAll(int page = 1, int pageSize = 20, 
            string? urlFilter = null, DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>
        /// Save scraped data with deduplication
        /// </summary>
        /// <param name="data">Data to save</param>
        /// <returns>Saved data with ID</returns>
        ScrapedData SaveData(ScrapedData data);

        /// <summary>
        /// Delete scraped data by ID
        /// </summary>
        /// <param name="id">ID of the data to delete</param>
        /// <returns>True if deleted, false if not found</returns>
        bool DeleteData(Guid id);

        /// <summary>
        /// Export data to the specified format
        /// </summary>
        /// <param name="request">Export request with format and filters</param>
        /// <returns>Stream containing the exported data</returns>
        Stream ExportData(ExportRequestDto request);

        /// <summary>
        /// Check if content already exists based on hash
        /// </summary>
        /// <param name="contentHash">Hash to check</param>
        /// <returns>True if content exists, otherwise false</returns>
        bool ContentExists(string contentHash);
    }

    /// <summary>
    /// Implementation of data service
    /// </summary>
    public class DataService : IDataService
    {
        private readonly ILiteDbContext _dbContext;
        private readonly ILogger<DataService> _logger;

        /// <summary>
        /// Create a new instance of the data service
        /// </summary>
        /// <param name="dbContext">LiteDB database context</param>
        /// <param name="logger">Logger</param>
        public DataService(ILiteDbContext dbContext, ILogger<DataService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Get scraped data by ID
        /// </summary>
        public ScrapedData? GetById(Guid id)
        {
            try
            {
                return _dbContext.ScrapedData.FindById(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving scraped data with ID {Id}", id);
                return null;
            }
        }

        /// <summary>
        /// Get all scraped data with optional filtering and pagination
        /// </summary>
        public (List<ScrapedData> Items, int TotalCount) GetAll(int page = 1, int pageSize = 20, 
            string? urlFilter = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                // Build query with filters
                var query = _dbContext.ScrapedData.Query();

                if (!string.IsNullOrEmpty(urlFilter))
                {
                    query = query.Where(x => x.Url.Contains(urlFilter));
                }

                if (startDate.HasValue)
                {
                    query = query.Where(x => x.ScrapedDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(x => x.ScrapedDate <= endDate.Value);
                }

                // Get total count
                var totalCount = query.Count();

                // Apply pagination and get results
                var results = query
                    .OrderByDescending(x => x.ScrapedDate)
                    .Skip((page - 1) * pageSize)
                    .Limit(pageSize)
                    .ToList();

                return (results, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving scraped data");
                return (new List<ScrapedData>(), 0);
            }
        }

        /// <summary>
        /// Save scraped data with deduplication
        /// </summary>
        public ScrapedData SaveData(ScrapedData data)
        {
            try
            {
                // Generate content hash if not provided
                if (string.IsNullOrEmpty(data.ContentHash))
                {
                    data.ContentHash = GenerateContentHash(data);
                }

                // Check for duplicates by hash
                var existingData = _dbContext.ScrapedData
                    .Query()
                    .Where(x => x.ContentHash == data.ContentHash)
                    .FirstOrDefault();

                if (existingData != null)
                {
                    _logger.LogInformation("Duplicate content found for URL {Url}, skipping save", data.Url);
                    return existingData;
                }

                // Save new data
                _dbContext.ScrapedData.Insert(data);
                _logger.LogInformation("Saved new scraped data with ID {Id} for URL {Url}", data.Id, data.Url);
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving scraped data for URL {Url}", data.Url);
                return data; // Return original data with error state
            }
        }

        /// <summary>
        /// Delete scraped data by ID
        /// </summary>
        public bool DeleteData(Guid id)
        {
            try
            {
                var result = _dbContext.ScrapedData.Delete(id);
                if (result)
                {
                    _logger.LogInformation("Deleted scraped data with ID {Id}", id);
                }
                else
                {
                    _logger.LogWarning("Failed to delete scraped data with ID {Id} - not found", id);
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting scraped data with ID {Id}", id);
                return false;
            }
        }

        /// <summary>
        /// Export data to the specified format
        /// </summary>
        public Stream ExportData(ExportRequestDto request)
        {
            try
            {
                // Get data using filters
                var (items, _) = GetAll(1, 10000, request.UrlFilter, request.StartDate, request.EndDate);
                
                // Create memory stream for result
                var stream = new MemoryStream();
                var writer = new StreamWriter(stream);

                // Export based on requested format
                switch (request.Format.ToLower())
                {
                    case "csv":
                        ExportToCsv(items, writer);
                        break;

                    case "html":
                        ExportToHtml(items, writer);
                        break;

                    case "json":
                    default:
                        ExportToJson(items, writer);
                        break;
                }

                writer.Flush();
                stream.Position = 0;
                return stream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting data");
                
                // Return empty stream with error message
                var stream = new MemoryStream();
                var writer = new StreamWriter(stream);
                writer.WriteLine("Error exporting data: " + ex.Message);
                writer.Flush();
                stream.Position = 0;
                return stream;
            }
        }

        /// <summary>
        /// Check if content already exists based on hash
        /// </summary>
        public bool ContentExists(string contentHash)
        {
            return _dbContext.ScrapedData
                .Query()
                .Where(x => x.ContentHash == contentHash)
                .Exists();
        }

        #region Helper Methods

        /// <summary>
        /// Generate content hash for deduplication
        /// </summary>
        /// <param name="data">Data to hash</param>
        /// <returns>SHA-256 hash of content</returns>
        private string GenerateContentHash(ScrapedData data)
        {
            // Combine key content fields for hashing
            var contentToHash = $"{data.Title}|{data.Content}";
            
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(contentToHash));
            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Export data to CSV format
        /// </summary>
        private void ExportToCsv(List<ScrapedData> items, TextWriter writer)
        {
            // Write CSV header
            writer.WriteLine("Id,Url,Title,ScrapedDate,ContentType,IsDynamicContent");
            
            // Write data rows
            foreach (var item in items)
            {
                writer.WriteLine($"\"{item.Id}\",\"{EscapeCsvField(item.Url)}\",\"{EscapeCsvField(item.Title)}\"," +
                                $"\"{item.ScrapedDate:yyyy-MM-dd HH:mm:ss}\",\"{item.ContentType}\",\"{item.IsDynamicContent}\"");
            }
        }

        /// <summary>
        /// Escape CSV field content
        /// </summary>
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;
                
            return field.Replace("\"", "\"\"");
        }

        /// <summary>
        /// Export data to JSON format
        /// </summary>
        private void ExportToJson(List<ScrapedData> items, TextWriter writer)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            
            var dtos = items.Select(item => new ScrapedDataDto
            {
                Id = item.Id,
                Url = item.Url,
                Title = item.Title,
                Content = item.Content,
                ContentPreview = TruncateContent(item.Content, 200),
                ScrapedDate = item.ScrapedDate,
                ContentType = item.ContentType,
                IsDynamicContent = item.IsDynamicContent,
                Metadata = string.IsNullOrEmpty(item.Metadata) ? new object() : 
                    JsonSerializer.Deserialize<object>(item.Metadata) ?? new object()
            }).ToList();
            
            JsonSerializer.Serialize(writer.BaseStream, dtos, options);
        }

        /// <summary>
        /// Export data to HTML format
        /// </summary>
        private void ExportToHtml(List<ScrapedData> items, TextWriter writer)
        {
            writer.WriteLine("<!DOCTYPE html>");
            writer.WriteLine("<html lang=\"en\">");
            writer.WriteLine("<head>");
            writer.WriteLine("  <meta charset=\"UTF-8\">");
            writer.WriteLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            writer.WriteLine("  <title>Exported Scraped Data</title>");
            writer.WriteLine("  <style>");
            writer.WriteLine("    table { border-collapse: collapse; width: 100%; }");
            writer.WriteLine("    th, td { border: 1px solid #ddd; padding: 8px; }");
            writer.WriteLine("    tr:nth-child(even) { background-color: #f2f2f2; }");
            writer.WriteLine("    th { padding-top: 12px; padding-bottom: 12px; text-align: left; background-color: #4CAF50; color: white; }");
            writer.WriteLine("  </style>");
            writer.WriteLine("</head>");
            writer.WriteLine("<body>");
            writer.WriteLine("  <h1>Exported Scraped Data</h1>");
            writer.WriteLine("  <table>");
            writer.WriteLine("    <tr>");
            writer.WriteLine("      <th>ID</th>");
            writer.WriteLine("      <th>URL</th>");
            writer.WriteLine("      <th>Title</th>");
            writer.WriteLine("      <th>Scraped Date</th>");
            writer.WriteLine("      <th>Content Preview</th>");
            writer.WriteLine("    </tr>");

            foreach (var item in items)
            {
                writer.WriteLine("    <tr>");
                writer.WriteLine($"      <td>{item.Id}</td>");
                writer.WriteLine($"      <td><a href=\"{HtmlEncode(item.Url)}\" target=\"_blank\">{HtmlEncode(item.Url)}</a></td>");
                writer.WriteLine($"      <td>{HtmlEncode(item.Title)}</td>");
                writer.WriteLine($"      <td>{item.ScrapedDate:yyyy-MM-dd HH:mm:ss}</td>");
                writer.WriteLine($"      <td>{HtmlEncode(TruncateContent(item.Content, 200))}</td>");
                writer.WriteLine("    </tr>");
            }

            writer.WriteLine("  </table>");
            writer.WriteLine("</body>");
            writer.WriteLine("</html>");
        }

        /// <summary>
        /// Encode string for HTML output
        /// </summary>
        private string HtmlEncode(string text)
        {
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

        /// <summary>
        /// Truncate content to specified length
        /// </summary>
        private string TruncateContent(string content, int maxLength)
        {
            if (string.IsNullOrEmpty(content) || content.Length <= maxLength)
                return content;
                
            return content.Substring(0, maxLength) + "...";
        }

        #endregion
    }
}