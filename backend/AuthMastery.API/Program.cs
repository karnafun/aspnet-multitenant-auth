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
    // SERILOG INTEGRATION
    // ============================================
    builder.Host.UseSerilog();

    // ============================================
    // DATABASE
    // ============================================
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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
    // ============================================
    // BUILD APP
    // ============================================
    var app = builder.Build();
    app.UseMiddleware<ExceptionHandlingMiddleware>();


    // ============================================
    // DATABASE SEEDING
    // ============================================
    await SeedDatabaseAsync(app);

    // ============================================
    // MIDDLEWARE PIPELINE
    // ============================================
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // Enrich all logs in the request with IP address
    app.Use(async (context, next) =>
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        using (LogContext.PushProperty("IpAddress", ipAddress))
        {
            await next();
        }
    });

    app.UseHttpsRedirection();
    app.UseSerilogRequestLogging();
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
        var seeder = new DbSeeder(context, userManager,logger, httpContext, roleManager);
        await seeder.SeedAsync();

        Log.Information("Database seeded successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while seeding the database");
        throw; // Re-throw so app doesn't start with bad data
    }
}