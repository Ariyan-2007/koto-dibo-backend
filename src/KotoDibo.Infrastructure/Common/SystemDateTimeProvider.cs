using KotoDibo.Application.Common.Interfaces;

namespace KotoDibo.Infrastructure.Common;

public class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
