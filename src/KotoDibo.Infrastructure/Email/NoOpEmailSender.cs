using KotoDibo.Application.Common.Interfaces;

namespace KotoDibo.Infrastructure.Email;

public class NoOpEmailSender : IEmailSender
{
    public Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
