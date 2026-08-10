using System.Text;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Infrastructure.Auth;
using KotoDibo.Infrastructure.Common;
using KotoDibo.Infrastructure.Email;
using KotoDibo.Infrastructure.Persistence.MongoDb;
using KotoDibo.Infrastructure.Persistence.MongoDb.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KotoDibo.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoDbSettings>(configuration.GetSection(MongoDbSettings.SectionName));

        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .Validate(s => Encoding.UTF8.GetByteCount(s.Secret ?? string.Empty) >= 32,
                "Jwt:Secret must be configured and at least 32 bytes (256 bits) long.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.Issuer), "Jwt:Issuer must be configured.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.Audience), "Jwt:Audience must be configured.")
            .Validate(s => s.ExpiryMinutes > 0, "Jwt:ExpiryMinutes must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<RefreshTokenSettings>()
            .Bind(configuration.GetSection(RefreshTokenSettings.SectionName))
            .Validate(s => s.ExpiryDays > 0, "RefreshToken:ExpiryDays must be greater than zero.")
            .ValidateOnStart();
        services.AddSingleton<IRefreshTokenSettings>(sp => sp.GetRequiredService<IOptions<RefreshTokenSettings>>().Value);

        services.AddSingleton<MongoDbContext>();
        services.AddScoped(typeof(IRepository<>), typeof(MongoRepository<>));

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IEmailSender, NoOpEmailSender>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }
}
