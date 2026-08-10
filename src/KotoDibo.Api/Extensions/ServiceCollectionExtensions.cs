using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using KotoDibo.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace KotoDibo.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public const string AuthRateLimiterPolicy = "auth";

    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = !environment.IsDevelopment();

                // Without this, the handler silently remaps short JWT claim names ("sub", "email")
                // to legacy long-form ClaimTypes URIs on validation, so code reading claims by
                // their issued names (e.g. JwtRegisteredClaimNames.Sub) finds nothing post-auth.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireExpirationTime = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorization();

        services.AddCors(options =>
        {
            options.AddPolicy("Default", policy =>
            {
                var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                        .AllowCredentials();
                }

                // No origins configured => no cross-origin requests are allowed (safe default).
            });
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Brute-force / credential-stuffing protection on register & login: a handful of
            // attempts per minute per client IP, no queueing (excess requests are rejected immediately).
            options.AddPolicy(AuthRateLimiterPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/json";
                var payload = JsonSerializer.Serialize(new
                {
                    status = StatusCodes.Status429TooManyRequests,
                    title = "Too many requests. Please try again later.",
                });
                await context.HttpContext.Response.WriteAsync(payload, cancellationToken);
            };
        });

        return services;
    }
}
