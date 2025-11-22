using Serilog;
using AuthMastery.API.Data;
using AuthMastery.API.Models;
using AuthMastery.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Serilog.Context;
using Hellang.Middleware.ProblemDetails;
using AuthMastery.API.Enums;
using AuthMastery.API.Mappings;
using AutoMapper;
using Microsoft.AspNetCore.Diagnostics;
using AuthMastery.API.DTO;
using Microsoft.AspNetCore.Authorization;
using AuthMastery.API.Extensions;
using Microsoft.Extensions.Options;
using System;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

// ============================================
// SERILOG CONFIGURATION (Bootstrap Logger)
// ============================================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)

    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .Enrich.WithMachineName()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine("logs", "log-.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30
    )
    .CreateLogger();

try
{
    Log.Information("Starting AuthMastery API");
    var builder = WebApplication.CreateBuilder(args);

    // ============================================
    // CONFIGURATION VALIDATION
    // ============================================
    // Validate required configuration values early to fail fast
    builder.Configuration.ValidateRequiredConfiguration();

    // ============================================
    // SERILOG INTEGRATION
    // ============================================
    builder.Host.UseSerilog();

    // ============================================
    // DATABASE
    // ============================================
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            var connStr = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING")
                      ?? builder.Configuration.GetConnectionString("DefaultConnection");
            options.UseSqlServer(connStr);
        });

    builder.Services.AddHealthChecks()
      .AddDbContextCheck<ApplicationDbContext>("database");


    // ============================================
    // IDENTITY
    // ============================================
    builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    // ============================================
    // SERVICES + AUTHENTICATION (JWT)
    // ============================================

    builder.Services.AddAuthMasteryServices();
    builder.Services.AddJwtAuthentication(builder.Configuration);

    builder.Services.AddAutoMapper(typeof(MappingProfile));

    // ============================================
    // API CONTROLLERS
    // ============================================
    builder.Services.AddControllers();
    // ============================================
    // SWAGGER / OPENAPI
    // ============================================
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT token"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(443, listenOptions =>
        {
            // Use configured certificate path and password, or fall back to development defaults
            var certPath = builder.Configuration[ConfigurationKeys.HttpsCertificatePath] ?? "/https/aspnetapp.pfx";
            var certPassword = builder.Configuration[ConfigurationKeys.HttpsCertificatePassword] ?? "DevCertPassword";
            
            listenOptions.UseHttps(certPath, certPassword);
        });
    });

    builder.Services.AddCors(options =>
    {
        // Get CORS origins from configuration, or use development defaults
        var corsOrigins = builder.Configuration.GetSection(ConfigurationKeys.CorsOrigins).Get<string[]>()
            ?? new[] { "http://localhost:3000", "https://localhost:3001" };
        
        options.AddPolicy("AllowSPA",
            policy => policy
                .WithOrigins(corsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
    });


    // ============================================
    // EXCEPTION HANDLING
    // ============================================
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = ctx =>
        {
            // Always include these
            ctx.ProblemDetails.Instance = $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}";
            ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;

            // NEVER include exception details in production
            if (!builder.Environment.IsDevelopment())
            {
                ctx.ProblemDetails.Extensions.Remove("exception");
                ctx.ProblemDetails.Extensions.Remove("exceptionDetails");
            }
        };
    });

    builder.Services.AddRateLimiter(options =>
    {
        // Global rate limit: 100 requests per minute per IP
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: partition => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0 // Don't queue, reject immediately
                }
            )
        );

        // Specific policy for auth endpoints
        options.AddFixedWindowLimiter("auth", options =>
        {
            options.PermitLimit = 5; // Only 5 login attempts
            options.Window = TimeSpan.FromMinutes(1);
            options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            options.QueueLimit = 0;
        });

        // What to return when rate limited
        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            // Tell client when they can retry
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
    // ============================================
    // BUILD APP
    // ============================================
    var app = builder.Build();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseCors("AllowSPA");

    // ============================================
    // DATABASE SEEDING
    // ============================================
    var shouldSeed = app.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("SEED_DATABASE", false);

    if (shouldSeed)
    {
        Log.Information("Seeding database (Environment: {Env}, SEED_DATABASE: {Flag})",
            app.Environment.EnvironmentName,
            builder.Configuration.GetValue<bool>("SEED_DATABASE", false));

        await SeedDatabaseAsync(app);
    }
    else
    {
        Log.Information("Skipping database seeding (SEED_DATABASE not set)");
    }

    // ============================================
    // MIDDLEWARE PIPELINE
    // ============================================
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }


    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append(
            "Content-Security-Policy",
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:;"
        );
        // Enrich all logs in the request with IP address
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        using (LogContext.PushProperty("IpAddress", ipAddress))
        {
            await next();
        }
    });
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";

            var result = JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.ToString()
                }),
                totalDuration = report.TotalDuration.ToString()
            });

            await context.Response.WriteAsync(result);
        }
    });

    app.UseHttpsRedirection();
    app.UseSerilogRequestLogging(options =>
    {
        options.GetLevel = (httpContext, _, __) =>
        {
            if (httpContext.Request.Path.StartsWithSegments("/health"))
                return Serilog.Events.LogEventLevel.Debug;

            return Serilog.Events.LogEventLevel.Information;
        };
    });
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// ============================================
// HELPER METHODS
// ============================================
static async Task SeedDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILogger<DbSeeder>>();
        var httpContext = services.GetRequiredService<IHttpContextAccessor>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var seeder = new DbSeeder(context, userManager, logger, httpContext, roleManager);
        await seeder.SeedAsync();

        Log.Information("Database seeded successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while seeding the database");
        throw; // Re-throw so app doesn't start with bad data
    }
}