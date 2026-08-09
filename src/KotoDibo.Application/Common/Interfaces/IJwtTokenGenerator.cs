using KotoDibo.Domain.Entities;

namespace KotoDibo.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
