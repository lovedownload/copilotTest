using System;

namespace copilotTest.Models
{
    /// <summary>
    /// Represents data scraped from websites
    /// </summary>
    public class ScrapedData
    {
        /// <summary>
        /// Unique identifier for the scraped data
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// URL of the scraped page
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Title of the scraped page
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Main content of the scraped page
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Metadata extracted from the page (JSON format)
        /// </summary>
        public string Metadata { get; set; } = "{}";

        /// <summary>
        /// Date when the data was scraped
        /// </summary>
        public DateTime ScrapedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Content hash used for deduplication
        /// </summary>
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>
        /// Content type (HTML, JSON, etc.)
        /// </summary>
        public string ContentType { get; set; } = "text/html";

        /// <summary>
        /// Flag indicating if the data was scraped using dynamic rendering
        /// </summary>
        public bool IsDynamicContent { get; set; } = false;

        /// <summary>
        /// Status code from the scraping request
        /// </summary>
        public int StatusCode { get; set; } = 200;
    }
}