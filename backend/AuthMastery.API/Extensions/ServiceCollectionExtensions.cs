using AuthMastery.API.Enums;
using AuthMastery.API.Models;
using AuthMastery.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace AuthMastery.API.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAuthMasteryServices(this IServiceCollection services)
        {
            services.AddScoped<TokenService>();
            services.AddScoped<AuthService>();
            services.AddScoped<ProjectService>();
            services.AddScoped<TagService>();
            services.AddScoped<UserService>();
            services.AddScoped<ITenantProvider, TenantProvider>();
            services.AddScoped<IAuthorizationHandler, CanManageProjectsHandler>();
            services.AddHttpContextAccessor();
            return services;
        }

        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services,IConfiguration configuration)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                        {
                           ValidateIssuerSigningKey = true,
                           IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration[ConfigurationKeys.JwtSecret]!)),
                           ValidateIssuer = true,
                           ValidIssuer = configuration[ConfigurationKeys.JwtIssuer],
                           ValidateAudience = true,
                           ValidAudience = configuration[ConfigurationKeys.JwtAudience],
                           ValidateLifetime = true,
                           ClockSkew = TimeSpan.Zero
                       };
                });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("CanManageProjects", policy =>
                {
                    policy.Requirements.Add(new CanManageProjectsRequirement());
                });
            });
            return services;
        }
    }
}
