using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Infrastructure.Auth;
using KotoDibo.Infrastructure.BackgroundServices;
using KotoDibo.Infrastructure.Common;
using KotoDibo.Infrastructure.Email;
using KotoDibo.Infrastructure.Persistence.MongoDb;
using KotoDibo.Infrastructure.Persistence.MongoDb.Repositories;
using KotoDibo.Infrastructure.Storage;
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

        services.AddOptions<InviteSettings>()
            .Bind(configuration.GetSection(InviteSettings.SectionName))
            .Validate(s => !string.IsNullOrWhiteSpace(s.BaseUrl), "Invites:BaseUrl must be configured.")
            .ValidateOnStart();
        services.AddSingleton<IInviteSettings>(sp => sp.GetRequiredService<IOptions<InviteSettings>>().Value);

        services.Configure<R2Settings>(configuration.GetSection(R2Settings.SectionName));
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var r2 = sp.GetRequiredService<IOptions<R2Settings>>().Value;
            var config = new AmazonS3Config
            {
                ServiceURL = r2.Endpoint,
                ForcePathStyle = true,
                // R2 doesn't support the AWS SDK v4 default of streaming trailer checksums; falling
                // back to "only when the API requires one" avoids PutObject failing against it.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            };
            return new AmazonS3Client(r2.AccessKeyId, r2.SecretAccessKey, config);
        });
        services.AddScoped<IFileStorageService, R2StorageService>();
        services.AddSingleton<IQrCodeService, QrCodeService>();

        services.AddSingleton<MongoDbContext>();
        services.AddScoped(typeof(IRepository<>), typeof(MongoRepository<>));

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IEmailSender, NoOpEmailSender>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddHostedService<RecurringExpenseGenerationHostedService>();

        return services;
    }
}
