using KotoDibo.Domain.Entities;

namespace KotoDibo.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    (string AccessToken, DateTime ExpiresAt) GenerateToken(User user);
}
