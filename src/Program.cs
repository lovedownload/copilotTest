using copilotTest.Infrastructure;
using copilotTest.Services;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.LiteDB;
using Microsoft.Playwright;
using Serilog;
using System.IO;

namespace copilotTest
{
    /// <summary>
    /// Main program class
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Entry point for the application
        /// </summary>
        /// <param name="args">Command line arguments</param>
        public static void Main(string[] args)
        {
            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("logs/copilotTest-.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                Log.Information("Starting web application");
                
                var builder = WebApplication.CreateBuilder(args);
                
                // Add Serilog
                builder.Host.UseSerilog();

                // Add services to the container
                ConfigureServices(builder.Services, builder.Configuration);
                
                var app = builder.Build();

                // Configure middleware
                ConfigureMiddleware(app);
                
                // Install Playwright browsers if not present
                InstallPlaywrightBrowsersAsync().Wait();
                
                // Run the app
                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        /// Configure application services
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">App configuration</param>
        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Add controllers and API explorer
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            
            // Add HTTP client
            services.AddHttpClient();

            // Configure LiteDB
            services.Configure<LiteDbOptions>(options =>
            {
                options.DatabasePath = configuration["Database:Path"] ?? "Data/ScrapedData.db";
            });

            // Add LiteDB context
            services.AddSingleton<ILiteDbContext, LiteDbContext>();

            // Add application services
            services.AddScoped<IDataService, DataService>();
            services.AddScoped<IScraperService, ScraperService>();

            // Configure Hangfire with LiteDB
            services.AddHangfire(config =>
            {
                config.UseLiteDbStorage(configuration["Database:HangfirePath"] ?? "Data/Hangfire.db");
            });
            services.AddHangfireServer();

            // Add CORS
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });
        }

        /// <summary>
        /// Configure application middleware
        /// </summary>
        /// <param name="app">Web application</param>
        private static void ConfigureMiddleware(WebApplication app)
        {
            // Enable Swagger in development
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // Create data directory if it doesn't exist
            var dataDir = Path.Combine(app.Environment.ContentRootPath, "Data");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }

            // Create logs directory if it doesn't exist
            var logsDir = Path.Combine(app.Environment.ContentRootPath, "logs");
            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }

            // Use HTTPS redirection
            app.UseHttpsRedirection();
            
            // Use CORS
            app.UseCors();
            
            // Use authentication and authorization
            app.UseAuthentication();
            app.UseAuthorization();
            
            // Map API controllers
            app.MapControllers();
            
            // Configure Hangfire dashboard
            app.UseHangfireDashboard("/hangfire", new DashboardOptions
            {
                Authorization = new[] { new HangfireDashboardAuthorizationFilter() }
            });
        }

        /// <summary>
        /// Install Playwright browsers
        /// </summary>
        private static async Task InstallPlaywrightBrowsersAsync()
        {
            try
            {
                Log.Information("Installing Playwright browsers...");
                var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
                if (exitCode != 0)
                {
                    Log.Warning("Playwright browser installation returned non-zero exit code: {ExitCode}", exitCode);
                }
                else
                {
                    Log.Information("Playwright browsers installed successfully");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to install Playwright browsers");
            }
        }
    }

    /// <summary>
    /// Simple authorization filter for Hangfire dashboard
    /// </summary>
    public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            // In production, add proper authentication
            // For now, allow access in development environment
            var httpContext = context.GetHttpContext();
            return httpContext.Request.Host.Host == "localhost" || 
                  httpContext.Request.Host.Host == "127.0.0.1";
        }
    }
}