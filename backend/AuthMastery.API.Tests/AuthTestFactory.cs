using AuthMastery.API.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using Microsoft.Data.Sqlite;

public class AuthTestFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set environment to Testing to prevent database seeder from running
        builder.UseEnvironment("Testing");

        // Add test configuration
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Jwt:Secret"] = "ThisIsATestSecretKeyThatIsAtLeast32CharactersLongForHS256Algorithm",
                ["Jwt:RefreshTokenSecret"] = "ThisIsATestRefreshTokenSecretKeyThatIsAtLeast32CharactersLong",
                ["Jwt:Issuer"] = "https://localhost:5000",
                ["Jwt:Audience"] = "https://localhost:5000",
                ["Jwt:AccessTokenExpirationMinutes"] = "15",
                ["Jwt:RefreshTokenExpirationDays"] = "7"
            });
        });

        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        builder.ConfigureServices(services =>
        {
            // Remove the production DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Add SQLite in-memory database for tests using shared connection
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove the RateLimiterOptions service registration
            var rateLimiterOptionsDescriptor = services
                .FirstOrDefault(d => d.ServiceType == typeof(RateLimiterOptions));
            if (rateLimiterOptionsDescriptor != null)
                services.Remove(rateLimiterOptionsDescriptor);

            // Remove all IConfigureOptions<RateLimiterOptions> registrations
            var configureOptionsDescriptors = services
                .Where(s => s.ServiceType == typeof(IConfigureOptions<RateLimiterOptions>))
                .ToList();
            foreach (var descriptor in configureOptionsDescriptors)
                services.Remove(descriptor);

            // Now re-add rate limiter with unlimited everything
            services.AddRateLimiter(options =>
            {
                // Unlimited global limiter
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                    RateLimitPartition.GetNoLimiter("unlimited"));

                // Re-add auth policy with unlimited limits
                options.AddFixedWindowLimiter("auth", limiterOptions =>
                {
                    limiterOptions.PermitLimit = int.MaxValue;
                    limiterOptions.Window = TimeSpan.FromSeconds(1);
                    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiterOptions.QueueLimit = 0;
                });

                // Keep the OnRejected handler to avoid null reference issues
                options.OnRejected = async (context, token) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();
                    }
                    await context.HttpContext.Response.WriteAsJsonAsync(new
                    {
                        error = "Too many requests. Please try again later.",
                        retryAfter = retryAfter.TotalSeconds
                    }, cancellationToken: token);
                };
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Close();
            _connection?.Dispose();
        }
        base.Dispose(disposing);
    }
}