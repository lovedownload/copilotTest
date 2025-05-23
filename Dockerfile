# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["src/copilotTest.csproj", "src/"]
RUN dotnet restore "src/copilotTest.csproj"

# Copy the rest of the files and build
COPY ["src/", "src/"]
RUN dotnet build "src/copilotTest.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "src/copilotTest.csproj" -c Release -o /app/publish

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install required dependencies for Playwright
RUN apt-get update && apt-get install -y \
    wget \
    curl \
    libgssapi-krb5-2 \
    libx11-6 \
    libx11-xcb1 \
    libxcb1 \
    libxcomposite1 \
    libxcursor1 \
    libxdamage1 \
    libxext6 \
    libxfixes3 \
    libxi6 \
    libxrandr2 \
    libxrender1 \
    libxss1 \
    libxtst6 \
    libglib2.0-0 \
    libnss3 \
    libcups2 \
    libdrm2 \
    libdbus-1-3 \
    xvfb \
    fonts-liberation \
    libasound2 \
    libatk-bridge2.0-0 \
    libatk1.0-0 \
    libgtk-3-0 \
    && rm -rf /var/lib/apt/lists/*

# Copy published files from publish stage
COPY --from=publish /app/publish .

# Install Playwright browsers during build
ENV PLAYWRIGHT_BROWSERS_PATH="/app/ms-playwright"
RUN dotnet tool install --tool-path /tools Microsoft.Playwright.CLI
RUN /tools/playwright install chromium --with-deps
RUN chmod -R 777 /app/ms-playwright

# Create necessary directories
RUN mkdir -p /app/Data /app/logs
RUN chmod -R 777 /app/Data /app/logs

# Expose port
EXPOSE 8080
ENV ASPNETCORE_URLS="http://+:8080"

# Set the entry point
ENTRYPOINT ["dotnet", "copilotTest.dll"]