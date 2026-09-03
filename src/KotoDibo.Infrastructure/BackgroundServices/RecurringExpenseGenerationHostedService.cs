using KotoDibo.Application.Common;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.RecurringExpenses.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KotoDibo.Infrastructure.BackgroundServices;

// Periodically materializes every user's due recurring expenses into real Expense rows, so
// "upcoming" recurring items actually turn into tracked spend without anyone having to open the
// app and trigger it manually. Runs on a fixed interval rather than being scheduled per-occurrence
// (there's no job scheduler in this stack) — safe to run as often as this because generation is
// idempotent (see RecurringExpenseGenerator/RecurringExpenseService).
public class RecurringExpenseGenerationHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecurringExpenseGenerationHostedService> _logger;

    public RecurringExpenseGenerationHostedService(IServiceScopeFactory scopeFactory, ILogger<RecurringExpenseGenerationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var recurringExpenseService = scope.ServiceProvider.GetRequiredService<IRecurringExpenseService>();
            var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            var today = LocalDate.TodayFor(dateTimeProvider.UtcNow);
            await recurringExpenseService.GenerateDueOccurrencesForAllUsersAsync(today, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown in progress — not an error.
        }
        catch (Exception exception)
        {
            // One bad sweep must not permanently kill the timer loop — log and let the next tick retry.
            _logger.LogError(exception, "Recurring expense generation sweep failed.");
        }
    }
}
