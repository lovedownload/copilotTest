using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace copilotTest.Models
{
    /// <summary>
    /// Request DTO for scraping operations
    /// </summary>
    public class ScrapingRequestDto
    {
        /// <summary>
        /// URL to scrape
        /// </summary>
        [Required]
        [Url]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Whether to use dynamic content scraping (Playwright)
        /// </summary>
        public bool UseDynamicScraping { get; set; } = false;

        /// <summary>
        /// Wait time in milliseconds for dynamic page loading
        /// </summary>
        public int WaitTimeMs { get; set; } = 5000;

        /// <summary>
        /// Custom CSS selectors to extract specific content
        /// </summary>
        public Dictionary<string, string> Selectors { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Data transfer object for scraped data
    /// </summary>
    public class ScrapedDataDto
    {
        /// <summary>
        /// Unique identifier for the scraped data
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// URL of the scraped page
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Title of the scraped page
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Preview of the content (truncated)
        /// </summary>
        public string ContentPreview { get; set; } = string.Empty;

        /// <summary>
        /// Full content of the scraped page
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Metadata extracted from the page
        /// </summary>
        public object Metadata { get; set; } = new object();

        /// <summary>
        /// Date when the data was scraped
        /// </summary>
        public DateTime ScrapedDate { get; set; }

        /// <summary>
        /// Content type of the scraped data
        /// </summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if content was scraped using dynamic rendering
        /// </summary>
        public bool IsDynamicContent { get; set; }
    }

    /// <summary>
    /// Request DTO for exporting data
    /// </summary>
    public class ExportRequestDto
    {
        /// <summary>
        /// Export format (csv, json, html)
        /// </summary>
        [Required]
        public string Format { get; set; } = "json";

        /// <summary>
        /// Start date for filtering data
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// End date for filtering data
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// URL filter (partial match)
        /// </summary>
        public string? UrlFilter { get; set; }

        /// <summary>
        /// Content filter (partial match)
        /// </summary>
        public string? ContentFilter { get; set; }
    }

    /// <summary>
    /// Response with paged results of scraped data
    /// </summary>
    public class PagedScrapedDataResponseDto
    {
        /// <summary>
        /// List of scraped data items
        /// </summary>
        public List<ScrapedDataDto> Items { get; set; } = new List<ScrapedDataDto>();

        /// <summary>
        /// Total count of all matching items
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page number
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; }
    }
}