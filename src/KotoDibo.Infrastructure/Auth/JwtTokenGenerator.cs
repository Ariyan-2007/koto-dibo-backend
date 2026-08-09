using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Domain.Entities;

namespace KotoDibo.Infrastructure.Auth;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    public string GenerateToken(User user) => throw new NotImplementedException();
}
