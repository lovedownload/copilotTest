using copilotTest.Models;
using LiteDB;
using Microsoft.Extensions.Options;
using System;
using System.IO;

namespace copilotTest.Infrastructure
{
    /// <summary>
    /// Configuration options for LiteDB
    /// </summary>
    public class LiteDbOptions
    {
        /// <summary>
        /// Database file path
        /// </summary>
        public string DatabasePath { get; set; } = "ScrapedData.db";

        /// <summary>
        /// Connection string for LiteDB
        /// </summary>
        public string ConnectionString => $"Filename={DatabasePath};Connection=shared";
    }

    /// <summary>
    /// LiteDB database context
    /// </summary>
    public interface ILiteDbContext
    {
        /// <summary>
        /// Collection of scraped data
        /// </summary>
        ILiteCollection<ScrapedData> ScrapedData { get; }
    }

    /// <summary>
    /// Implementation of LiteDB context
    /// </summary>
    public class LiteDbContext : ILiteDbContext, IDisposable
    {
        private readonly LiteDatabase _database;
        private bool _disposed = false;

        /// <summary>
        /// Creates a new instance of the LiteDB context
        /// </summary>
        /// <param name="options">LiteDB configuration options</param>
        public LiteDbContext(IOptions<LiteDbOptions> options)
        {
            // Ensure the directory exists
            var dbPath = options.Value.DatabasePath;
            var directory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Set up the database
            _database = new LiteDatabase(options.Value.ConnectionString);
            
            // Ensure collections exist and set up indexes
            SetupCollections();
        }

        /// <summary>
        /// Constructor for testing with an in-memory database
        /// </summary>
        /// <param name="database">In-memory LiteDB instance</param>
        public LiteDbContext(LiteDatabase database)
        {
            _database = database;
            SetupCollections();
        }

        /// <summary>
        /// Collection of scraped data
        /// </summary>
        public ILiteCollection<ScrapedData> ScrapedData => _database.GetCollection<ScrapedData>("scraped_data");

        /// <summary>
        /// Setup collections and indexes
        /// </summary>
        private void SetupCollections()
        {
            // Set up indexes for deduplication and querying
            var collection = ScrapedData;
            collection.EnsureIndex(x => x.ContentHash, unique: true);
            collection.EnsureIndex(x => x.Url);
            collection.EnsureIndex(x => x.ScrapedDate);
        }

        /// <summary>
        /// Disposes the database connection
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the database connection
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _database?.Dispose();
            }

            _disposed = true;
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~LiteDbContext()
        {
            Dispose(false);
        }
    }
}