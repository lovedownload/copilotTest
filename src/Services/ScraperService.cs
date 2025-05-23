using copilotTest.Models;
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace copilotTest.Services
{
    /// <summary>
    /// Service for web scraping operations
    /// </summary>
    public interface IScraperService
    {
        /// <summary>
        /// Scrapes a webpage asynchronously
        /// </summary>
        /// <param name="request">Scraping request with URL and options</param>
        /// <returns>Scraped data from the webpage</returns>
        Task<ScrapedData> ScrapeUrlAsync(ScrapingRequestDto request);

        /// <summary>
        /// Scrapes a webpage using static HTTP request (no JavaScript execution)
        /// </summary>
        /// <param name="url">URL to scrape</param>
        /// <returns>Scraped data from the webpage</returns>
        Task<ScrapedData> ScrapeStaticAsync(string url);

        /// <summary>
        /// Scrapes a webpage with dynamic content using Playwright
        /// </summary>
        /// <param name="request">Scraping request with URL and options</param>
        /// <returns>Scraped data with dynamically rendered content</returns>
        Task<ScrapedData> ScrapeDynamicAsync(ScrapingRequestDto request);
    }

    /// <summary>
    /// Implementation of scraper service
    /// </summary>
    public class ScraperService : IScraperService
    {
        private readonly IDataService _dataService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ScraperService> _logger;

        /// <summary>
        /// Creates a new instance of the scraper service
        /// </summary>
        /// <param name="dataService">Data service for storing scraped data</param>
        /// <param name="httpClientFactory">HTTP client factory</param>
        /// <param name="logger">Logger</param>
        public ScraperService(
            IDataService dataService,
            IHttpClientFactory httpClientFactory,
            ILogger<ScraperService> logger)
        {
            _dataService = dataService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Scrapes a webpage asynchronously
        /// </summary>
        public async Task<ScrapedData> ScrapeUrlAsync(ScrapingRequestDto request)
        {
            try
            {
                _logger.LogInformation("Starting scraping for URL: {Url}", request.Url);

                // Choose scraping method based on request
                ScrapedData result;
                if (request.UseDynamicScraping)
                {
                    _logger.LogInformation("Using dynamic scraping for URL: {Url}", request.Url);
                    result = await ScrapeDynamicAsync(request);
                }
                else
                {
                    _logger.LogInformation("Using static scraping for URL: {Url}", request.Url);
                    result = await ScrapeStaticAsync(request.Url);
                }

                // Generate content hash for deduplication
                result.ContentHash = GenerateContentHash(result);

                // Check for duplicates before saving
                if (_dataService.ContentExists(result.ContentHash))
                {
                    _logger.LogInformation("Duplicate content detected for URL: {Url}", request.Url);
                }
                else
                {
                    // Save non-duplicate data
                    _dataService.SaveData(result);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping URL: {Url}", request.Url);
                
                // Return a partial result with error information
                return new ScrapedData
                {
                    Url = request.Url,
                    Title = "Error: Scraping Failed",
                    Content = $"Failed to scrape URL: {ex.Message}",
                    StatusCode = 500,
                    ScrapedDate = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Scrapes a webpage using static HTTP request
        /// </summary>
        public async Task<ScrapedData> ScrapeStaticAsync(string url)
        {
            var client = _httpClientFactory.CreateClient();
            
            // Add user agent to avoid being blocked
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.110 Safari/537.36");
            
            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "text/html";
            
            var result = new ScrapedData
            {
                Url = url,
                Content = content,
                ContentType = contentType,
                StatusCode = (int)response.StatusCode,
                ScrapedDate = DateTime.UtcNow,
                IsDynamicContent = false
            };

            // Extract metadata depending on content type
            if (contentType.Contains("html"))
            {
                // Extract title and metadata from HTML
                result.Title = ExtractTitle(content);
                result.Metadata = ExtractMetadata(content);
            }
            else if (contentType.Contains("json"))
            {
                try
                {
                    // For JSON content, try to extract a title-like property
                    using var document = JsonDocument.Parse(content);
                    var root = document.RootElement;
                    
                    // Look for common title properties
                    foreach (var titleProp in new[] { "title", "name", "heading" })
                    {
                        if (root.TryGetProperty(titleProp, out var title))
                        {
                            result.Title = title.GetString() ?? url;
                            break;
                        }
                    }
                    
                    if (string.IsNullOrEmpty(result.Title))
                    {
                        result.Title = "JSON Data: " + url;
                    }

                    // The entire JSON is already stored in content
                    result.Metadata = content;
                }
                catch
                {
                    result.Title = "Invalid JSON: " + url;
                }
            }
            else
            {
                // Default title for other content types
                result.Title = $"{contentType} from {url}";
            }

            return result;
        }

        /// <summary>
        /// Scrapes a webpage with dynamic content using Playwright
        /// </summary>
        public async Task<ScrapedData> ScrapeDynamicAsync(ScrapingRequestDto request)
        {
            // Initialize Playwright
            using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            // Create a new context with viewport
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 720 },
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.110 Safari/537.36"
            });

            // Open a new page
            var page = await context.NewPageAsync();
            
            // Initialize result
            var result = new ScrapedData
            {
                Url = request.Url,
                ScrapedDate = DateTime.UtcNow,
                IsDynamicContent = true
            };

            try
            {
                // Navigate to URL and wait for load
                var response = await page.GotoAsync(request.Url);
                
                // Wait for dynamic content to load
                await page.WaitForTimeoutAsync(request.WaitTimeMs);
                
                result.StatusCode = response?.Status ?? 0;
                result.ContentType = response?.Headers.TryGetValue("content-type", out var contentType) == true
                    ? contentType
                    : "text/html";

                // Extract content from page
                result.Content = await page.ContentAsync();
                result.Title = await page.TitleAsync();

                // Extract metadata
                result.Metadata = await ExtractMetadataFromPage(page);

                // Extract custom selectors if provided
                if (request.Selectors != null && request.Selectors.Count > 0)
                {
                    var customData = new Dictionary<string, string>();
                    
                    foreach (var selector in request.Selectors)
                    {
                        try
                        {
                            var element = await page.QuerySelectorAsync(selector.Value);
                            if (element != null)
                            {
                                customData[selector.Key] = await element.TextContentAsync() ?? string.Empty;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to extract custom selector {Key}: {Selector}", 
                                selector.Key, selector.Value);
                        }
                    }
                    
                    // Add custom data to metadata
                    var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(result.Metadata);
                    if (metadata == null)
                    {
                        metadata = new Dictionary<string, object>();
                    }
                    
                    metadata["customSelectors"] = customData;
                    result.Metadata = JsonSerializer.Serialize(metadata);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during dynamic scraping of URL: {Url}", request.Url);
                result.Content = $"Dynamic scraping error: {ex.Message}";
                result.Title = "Error: Dynamic Scraping Failed";
                result.StatusCode = 500;
            }

            return result;
        }

        #region Helper Methods

        /// <summary>
        /// Extract title from HTML content
        /// </summary>
        private string ExtractTitle(string htmlContent)
        {
            if (string.IsNullOrEmpty(htmlContent))
                return string.Empty;

            // Extract title using regex
            var titleMatch = Regex.Match(htmlContent, @"<title>\s*(.+?)\s*</title>", RegexOptions.IgnoreCase);
            
            if (titleMatch.Success && titleMatch.Groups.Count > 1)
            {
                return titleMatch.Groups[1].Value.Trim();
            }
            
            // Try to find h1 if title not found
            var h1Match = Regex.Match(htmlContent, @"<h1[^>]*>\s*(.+?)\s*</h1>", RegexOptions.IgnoreCase);
            
            if (h1Match.Success && h1Match.Groups.Count > 1)
            {
                return h1Match.Groups[1].Value.Trim();
            }
            
            return string.Empty;
        }

        /// <summary>
        /// Extract metadata from HTML content
        /// </summary>
        private string ExtractMetadata(string htmlContent)
        {
            var metadata = new Dictionary<string, string>();
            
            if (string.IsNullOrEmpty(htmlContent))
                return JsonSerializer.Serialize(metadata);

            // Extract meta tags
            var metaPattern = @"<meta\s+(?:name|property)=[""']([^""']+)[""']\s+content=[""']([^""']+)[""']";
            var metaMatches = Regex.Matches(htmlContent, metaPattern, RegexOptions.IgnoreCase);
            
            foreach (Match match in metaMatches)
            {
                if (match.Groups.Count > 2)
                {
                    var name = match.Groups[1].Value.Trim();
                    var content = match.Groups[2].Value.Trim();
                    metadata[name] = content;
                }
            }
            
            return JsonSerializer.Serialize(metadata);
        }

        /// <summary>
        /// Extract metadata from a Playwright page
        /// </summary>
        private async Task<string> ExtractMetadataFromPage(IPage page)
        {
            var metadata = new Dictionary<string, string>();
            
            // Get all meta tags with name/property and content
            var metaTags = await page.EvaluateAsync<string[]>(@"
                Array.from(document.querySelectorAll('meta[name], meta[property]'))
                    .map(meta => {
                        const name = meta.getAttribute('name') || meta.getAttribute('property');
                        const content = meta.getAttribute('content');
                        return name && content ? `${name}|${content}` : null;
                    })
                    .filter(item => item !== null)
            ");
            
            foreach (var tag in metaTags)
            {
                var parts = tag.Split('|');
                if (parts.Length == 2)
                {
                    metadata[parts[0]] = parts[1];
                }
            }
            
            return JsonSerializer.Serialize(metadata);
        }

        /// <summary>
        /// Generate content hash for deduplication
        /// </summary>
        private string GenerateContentHash(ScrapedData data)
        {
            // Combine key content fields for hashing
            var contentToHash = $"{data.Title}|{data.Content}";
            
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(contentToHash));
            return Convert.ToBase64String(hashBytes);
        }

        #endregion
    }
}