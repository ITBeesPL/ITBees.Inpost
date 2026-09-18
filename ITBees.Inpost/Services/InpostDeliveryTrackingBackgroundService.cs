using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITBees.Inpost.Services;

/// <summary>
/// Sprawdzanie w tle, czy nadane przesyłki zostały już doręczone: co
/// <see cref="InpostDeliveryTrackingSettings.CheckInterval"/> jeden przebieg
/// <see cref="IInpostDeliveryTrackingService.CheckPendingShipmentsAsync"/>.
/// </summary>
public class InpostDeliveryTrackingBackgroundService : BackgroundService
{
    // Start aplikacji (migracje, rozgrzewka) nie czeka na ShipX ani nie konkuruje z nim o bazę.
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceProvider _serviceProvider;
    private readonly InpostDeliveryTrackingSettings _settings;
    private readonly ILogger<InpostDeliveryTrackingBackgroundService> _logger;

    public InpostDeliveryTrackingBackgroundService(IServiceProvider serviceProvider,
        InpostDeliveryTrackingSettings settings, ILogger<InpostDeliveryTrackingBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            return;
        }

        var interval = _settings.CheckInterval < MinInterval ? MinInterval : _settings.CheckInterval;

        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await CheckPendingShipmentsAsync(stoppingToken);
                await Task.Delay(interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Aplikacja się zamyka.
        }
    }

    private async Task CheckPendingShipmentsAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Serwis i repozytoria są krótko żyjące (DbContext aplikacji hosta) - osobny zakres na przebieg.
            using var scope = _serviceProvider.CreateScope();
            var tracking = scope.ServiceProvider.GetRequiredService<IInpostDeliveryTrackingService>();
            var result = await tracking.CheckPendingShipmentsAsync(stoppingToken);

            if (result.NotConfigured)
            {
                _logger.LogDebug("Integracja InPost nie jest skonfigurowana - pomijam sprawdzanie doręczeń");
                return;
            }

            // Cisza, gdy nic się nie zmieniło - przebieg co godzinę zaśmiecałby log.
            var level = result.Updated > 0 || result.Failed > 0 ? LogLevel.Information : LogLevel.Debug;
            _logger.Log(level,
                "Sprawdzanie doręczeń InPost: do sprawdzenia {Pending}, sprawdzono {Checked}, zmieniony stan {Updated}, " +
                "doręczone {Delivered}, błędy {Failed}{Aborted}{LastError}",
                result.Pending, result.Checked, result.Updated, result.Delivered, result.Failed,
                result.Aborted ? " (przerwano po kolejnych błędach - reszta w następnym przebiegu)" : string.Empty,
                string.IsNullOrWhiteSpace(result.LastError) ? string.Empty : $"; ostatni błąd: {result.LastError}");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Błąd sprawdzania doręczeń przesyłek InPost: {Message}", e.Message);
        }
    }
}
