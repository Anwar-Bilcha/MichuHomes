using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RadiantHomes.DAL;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Tests.Infrastructure
{
    /// <summary>
    /// A throw-away SQL Server database built from the migrations (so every run also proves the migration chain), with
    /// the permission catalog synced. Server: environment variable MICHU_TEST_SQL (a connection string without a
    /// database), else the developer connection string from RadiantHomes/appsettings.json. Dropped afterwards.
    /// </summary>
    public sealed class TestDatabase : IAsyncLifetime
    {
        public TestDatabase()
        {
            var server = Environment.GetEnvironmentVariable("MICHU_TEST_SQL");
            if (string.IsNullOrWhiteSpace(server))
            {
                server = DeveloperConnectionString() ?? throw new InvalidOperationException("Set MICHU_TEST_SQL to a SQL Server connection string.");
            }
            var builder = new SqlConnectionStringBuilder(server)
            {
                InitialCatalog = "MichuHomes_Tests_" + Guid.NewGuid().ToString("N")[..10],
                MultipleActiveResultSets = true,
                TrustServerCertificate = true,
            };
            ConnectionString = builder.ConnectionString;
        }

        public string ConnectionString { get; }

        public ApplicationDbContext NewContext(IHttpContextAccessor? http = null) =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(ConnectionString).Options, http);

        public async Task InitializeAsync()
        {
            await using var db = NewContext();
            await db.Database.MigrateAsync();
            await AccessControlSeeder.SyncAsync(db, NullLogger.Instance);
        }

        public async Task DisposeAsync()
        {
            SqlConnection.ClearAllPools();
            await using var db = NewContext();
            await db.Database.EnsureDeletedAsync();
        }

        private static string? DeveloperConnectionString()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "RadiantHomes", "appsettings.json"))) dir = dir.Parent;
            if (dir == null) return null;
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir.FullName, "RadiantHomes", "appsettings.json")));
            return json.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
        }
    }

    [CollectionDefinition(Name)]
    public sealed class DatabaseCollection : ICollectionFixture<TestDatabase>
    {
        public const string Name = "database";
    }
}
