using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AirlineReservation.Infrastructure.BackgroundServices;

/// <summary>
/// Periodic background service to safely purge expired seat holds from the database via ExecuteDeleteAsync.
/// Robust against unhandled exceptions per cycle to prevent terminating the ASP.NET Core host.
/// </summary>
public class HoldCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<HoldSettings> _holdSettings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<HoldCleanupService> _logger;

    public HoldCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<HoldSettings> holdSettings,
        TimeProvider timeProvider,
        ILogger<HoldCleanupService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _holdSettings = holdSettings ?? throw new ArgumentNullException(nameof(holdSettings));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_holdSettings.Value.CleanupEnabled)
        {
            _logger.LogInformation("HoldCleanupService is disabled via configuration (Hold:CleanupEnabled = false).");
            return;
        }

        _logger.LogInformation(
            "HoldCleanupService started with interval of {IntervalSeconds} seconds.",
            _holdSettings.Value.CleanupSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int deleted = await CleanupExpiredHoldsAsync(stoppingToken);
                if (deleted > 0)
                {
                    _logger.LogInformation("HoldCleanupService removed {Count} expired seat hold(s).", deleted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Isolate iteration failure: prevent background exceptions from killing the application host (.NET 6+ StopHost behavior)
                _logger.LogError(ex, "Unexpected error occurred during expired seat holds cleanup cycle.");
            }

            try
            {
                int intervalSeconds = _holdSettings.Value.CleanupSeconds > 0
                    ? _holdSettings.Value.CleanupSeconds
                    : 30;

                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("HoldCleanupService has stopped.");
    }

    /// <summary>
    /// Executes a single atomic cleanup pass using a dedicated DI scope and ExecuteDeleteAsync.
    /// Can also be called directly in integration tests or admin routines.
    /// </summary>
    public async Task<int> CleanupExpiredHoldsAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        return await unitOfWork.SeatHolds.DeleteAllExpiredHoldsAsync(utcNow, cancellationToken);
    }
}
