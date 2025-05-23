# copilotTest

A web scraping application built with .NET 8.0 that provides REST APIs for collecting, storing, and exporting web content. The application supports both static and dynamic web scraping approaches, with built-in features for data deduplication and background processing.

## Features

- **Multi-mode web scraping**:
  - Static scraping for traditional HTML/JSON content
  - Dynamic scraping using Playwright for JavaScript-rendered pages
  - Custom CSS selector support for targeted content extraction
- **Efficient data storage**:
  - LiteDB NoSQL database for scraped content
  - Content hashing for deduplication
  - Metadata storage for additional context
- **Background processing**:
  - Hangfire integration for queueing and scheduling scrape jobs
  - Batch URL processing support
- **Flexible data export**:
  - Export to CSV, JSON, or HTML formats
  - Filtering options by date range and URL

## Installation

### Prerequisites
- .NET 8.0 SDK
- Docker (optional, for containerized deployment)

### Local Development
1. Clone the repository
   ```
   git clone https://github.com/lovedownload/copilotTest.git
   cd copilotTest
   ```

2. Restore dependencies and build
   ```
   dotnet restore src/copilotTest.csproj
   dotnet build src/copilotTest.csproj
   ```

3. Run the application
   ```
   dotnet run --project src/copilotTest.csproj
   ```

### Docker Deployment
```
docker build -t copilottest .
docker run -p 8080:8080 copilottest
```

## Usage Examples

### Scrape a webpage (via API)
```
POST /api/data/scrape/background
Content-Type: application/json

{
  "url": "https://example.com",
  "useDynamicScraping": true,
  "selectors": {
    "title": "h1",
    "content": "main"
  }
}
```

### Export collected data
```
POST /api/data/export
Content-Type: application/json

{
  "format": "csv",
  "urlFilter": "example.com",
  "startDate": "2023-01-01T00:00:00Z"
}
```
