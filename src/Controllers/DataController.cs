using copilotTest.Models;
using copilotTest.Services;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace copilotTest.Controllers
{
    /// <summary>
    /// API controller for scraped data operations
    /// </summary>
    [ApiController]
    [Route("api/data")]
    public class DataController : ControllerBase
    {
        private readonly IDataService _dataService;
        private readonly IScraperService _scraperService;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly ILogger<DataController> _logger;

        /// <summary>
        /// Creates a new instance of the data controller
        /// </summary>
        /// <param name="dataService">Data service</param>
        /// <param name="scraperService">Scraper service</param>
        /// <param name="backgroundJobClient">Hangfire background job client</param>
        /// <param name="logger">Logger</param>
        public DataController(
            IDataService dataService,
            IScraperService scraperService,
            IBackgroundJobClient backgroundJobClient,
            ILogger<DataController> logger)
        {
            _dataService = dataService;
            _scraperService = scraperService;
            _backgroundJobClient = backgroundJobClient;
            _logger = logger;
        }

        /// <summary>
        /// Get all data with optional filtering
        /// </summary>
        /// <param name="page">Page number (defaults to 1)</param>
        /// <param name="pageSize">Page size (defaults to 20)</param>
        /// <param name="urlFilter">Optional URL filter</param>
        /// <param name="startDate">Optional start date</param>
        /// <param name="endDate">Optional end date</param>
        /// <returns>Paged list of scraped data</returns>
        [HttpGet]
        [ProducesResponseType(typeof(PagedScrapedDataResponseDto), 200)]
        public IActionResult GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? urlFilter = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                // Validate page and pageSize
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100;

                // Get data with pagination
                var (items, totalCount) = _dataService.GetAll(page, pageSize, urlFilter, startDate, endDate);

                // Map to DTOs
                var dtoItems = items.Select(MapToDto).ToList();

                // Create response
                var response = new PagedScrapedDataResponseDto
                {
                    Items = dtoItems,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving scraped data");
                return StatusCode(500, "An error occurred while retrieving data");
            }
        }

        /// <summary>
        /// Get data by ID
        /// </summary>
        /// <param name="id">Data ID</param>
        /// <returns>Scraped data</returns>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ScrapedDataDto), 200)]
        [ProducesResponseType(404)]
        public IActionResult GetById(Guid id)
        {
            try
            {
                var data = _dataService.GetById(id);
                if (data == null)
                {
                    return NotFound($"Data with ID {id} not found");
                }

                return Ok(MapToDto(data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving scraped data with ID {Id}", id);
                return StatusCode(500, "An error occurred while retrieving data");
            }
        }

        /// <summary>
        /// Delete data by ID
        /// </summary>
        /// <param name="id">Data ID</param>
        /// <returns>Success status</returns>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public IActionResult Delete(Guid id)
        {
            try
            {
                var success = _dataService.DeleteData(id);
                if (!success)
                {
                    return NotFound($"Data with ID {id} not found");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting scraped data with ID {Id}", id);
                return StatusCode(500, "An error occurred while deleting data");
            }
        }

        /// <summary>
        /// Scrape a URL synchronously
        /// </summary>
        /// <param name="request">Scraping request</param>
        /// <returns>Scraped data</returns>
        [HttpPost("scrape")]
        [ProducesResponseType(typeof(ScrapedDataDto), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> ScrapeUrl([FromBody] ScrapingRequestDto request)
        {
            try
            {
                if (!Uri.TryCreate(request.Url, UriKind.Absolute, out _))
                {
                    return BadRequest("Invalid URL format");
                }

                var scrapedData = await _scraperService.ScrapeUrlAsync(request);
                return Ok(MapToDto(scrapedData));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping URL {Url}", request.Url);
                return StatusCode(500, $"An error occurred during scraping: {ex.Message}");
            }
        }

        /// <summary>
        /// Queue a URL for background scraping
        /// </summary>
        /// <param name="request">Scraping request</param>
        /// <returns>Job ID</returns>
        [HttpPost("scrape/background")]
        [ProducesResponseType(typeof(string), 202)]
        [ProducesResponseType(400)]
        public IActionResult QueueScrapeUrl([FromBody] ScrapingRequestDto request)
        {
            try
            {
                if (!Uri.TryCreate(request.Url, UriKind.Absolute, out _))
                {
                    return BadRequest("Invalid URL format");
                }

                var jobId = _backgroundJobClient.Enqueue<IScraperService>(
                    service => service.ScrapeUrlAsync(request));

                return Accepted(new { JobId = jobId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error queueing scrape job for URL {Url}", request.Url);
                return StatusCode(500, $"An error occurred while queueing the job: {ex.Message}");
            }
        }

        /// <summary>
        /// Export data in requested format
        /// </summary>
        /// <param name="request">Export request</param>
        /// <returns>Exported data file</returns>
        [HttpPost("export")]
        [ProducesResponseType(200)]
        public IActionResult ExportData([FromBody] ExportRequestDto request)
        {
            try
            {
                // Validate export format
                var format = request.Format.ToLower();
                if (!new[] { "csv", "json", "html" }.Contains(format))
                {
                    return BadRequest("Unsupported export format. Supported formats: csv, json, html");
                }

                // Get content type based on format
                var contentType = format switch
                {
                    "csv" => "text/csv",
                    "html" => "text/html",
                    _ => "application/json"
                };

                // Get file name
                var fileName = $"scraped-data-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.{format}";

                // Export data
                var dataStream = _dataService.ExportData(request);

                // Return file
                return File(dataStream, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting data");
                return StatusCode(500, $"An error occurred during data export: {ex.Message}");
            }
        }

        /// <summary>
        /// Queue a batch of URLs for background scraping
        /// </summary>
        /// <param name="urls">List of URLs to scrape</param>
        /// <param name="useDynamicScraping">Whether to use dynamic scraping</param>
        /// <returns>List of job IDs</returns>
        [HttpPost("scrape/batch")]
        [ProducesResponseType(typeof(List<string>), 202)]
        [ProducesResponseType(400)]
        public IActionResult QueueBatchScrape(
            [FromBody] List<string> urls,
            [FromQuery] bool useDynamicScraping = false)
        {
            try
            {
                if (urls == null || !urls.Any())
                {
                    return BadRequest("No URLs provided for scraping");
                }

                var jobIds = new List<string>();

                foreach (var url in urls)
                {
                    if (Uri.TryCreate(url, UriKind.Absolute, out _))
                    {
                        var request = new ScrapingRequestDto
                        {
                            Url = url,
                            UseDynamicScraping = useDynamicScraping
                        };

                        var jobId = _backgroundJobClient.Enqueue<IScraperService>(
                            service => service.ScrapeUrlAsync(request));
                        
                        jobIds.Add(jobId);
                    }
                    else
                    {
                        _logger.LogWarning("Invalid URL format: {Url}, skipping", url);
                    }
                }

                if (!jobIds.Any())
                {
                    return BadRequest("No valid URLs were found in the request");
                }

                return Accepted(new { JobIds = jobIds });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error queueing batch scrape job");
                return StatusCode(500, $"An error occurred while queueing the jobs: {ex.Message}");
            }
        }

        #region Helper Methods

        /// <summary>
        /// Maps ScrapedData entity to DTO
        /// </summary>
        private ScrapedDataDto MapToDto(ScrapedData data)
        {
            return new ScrapedDataDto
            {
                Id = data.Id,
                Url = data.Url,
                Title = data.Title,
                ContentPreview = TruncateContent(data.Content, 200),
                Content = data.Content,
                ScrapedDate = data.ScrapedDate,
                ContentType = data.ContentType,
                IsDynamicContent = data.IsDynamicContent,
                Metadata = string.IsNullOrEmpty(data.Metadata) ? new object() :
                    System.Text.Json.JsonSerializer.Deserialize<object>(data.Metadata) ?? new object()
            };
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