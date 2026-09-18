using ITBees.Inpost.Entities;
using ITBees.Inpost.Models;
using ITBees.Interfaces.Repository;
using Microsoft.Extensions.Logging;

namespace ITBees.Inpost.Services;

public class InpostDeliveryTrackingService : IInpostDeliveryTrackingService
{
    /// <summary>
    /// Po tylu błędach ShipX z rzędu przebieg jest przerywany - to zwykle niedostępne API albo
    /// nieważny token, a nie problem pojedynczej przesyłki.
    /// </summary>
    private const int MaxConsecutiveFailures = 3;

    private readonly IInpostShipXClient _inpostShipXClient;
    private readonly IInpostIntegrationSettingsService _inpostIntegrationSettingsService;
    private readonly IReadOnlyRepository<InpostShipment> _shipmentRoRepo;
    private readonly IWriteOnlyRepository<InpostShipment> _shipmentWoRepo;
    private readonly InpostDeliveryTrackingSettings _settings;
    private readonly ILogger<InpostDeliveryTrackingService> _logger;

    public InpostDeliveryTrackingService(
        IInpostShipXClient inpostShipXClient,
        IInpostIntegrationSettingsService inpostIntegrationSettingsService,
        IReadOnlyRepository<InpostShipment> shipmentRoRepo,
        IWriteOnlyRepository<InpostShipment> shipmentWoRepo,
        InpostDeliveryTrackingSettings settings,
        ILogger<InpostDeliveryTrackingService> logger)
    {
        _inpostShipXClient = inpostShipXClient;
        _inpostIntegrationSettingsService = inpostIntegrationSettingsService;
        _shipmentRoRepo = shipmentRoRepo;
        _shipmentWoRepo = shipmentWoRepo;
        _settings = settings;
        _logger = logger;
    }

    public async Task<InpostDeliveryCheckResult> CheckPendingShipmentsAsync(CancellationToken ct = default)
    {
        var result = new InpostDeliveryCheckResult();
        var settings = _inpostIntegrationSettingsService.GetShipXSettingsOrNull();
        if (settings == null)
        {
            result.NotConfigured = true;
            return result;
        }

        var pending = GetShipmentsAwaitingCheck(DateTime.Now);
        result.Pending = pending.Count;

        var consecutiveFailures = 0;
        foreach (var shipment in pending)
        {
            ct.ThrowIfCancellationRequested();
            if (result.Checked > 0 && _settings.DelayBetweenRequests > TimeSpan.Zero)
            {
                await Task.Delay(_settings.DelayBetweenRequests, ct);
            }

            var state = await _inpostShipXClient.GetShipmentAsync(settings, shipment.InpostShipmentId!, ct);
            result.Checked++;

            if (!state.Success)
            {
                result.Failed++;
                result.LastError = state.ErrorMessage;
                _logger.LogDebug("Nie udało się sprawdzić stanu przesyłki InPost {ShipmentId}: {Error}",
                    shipment.InpostShipmentId, state.ErrorMessage);

                if (++consecutiveFailures >= MaxConsecutiveFailures)
                {
                    result.Aborted = true;
                    break;
                }

                continue;
            }

            consecutiveFailures = 0;
            if (SaveState(shipment, state))
            {
                result.Updated++;
            }

            if (InpostShipmentStatuses.IsDelivered(state.Status))
            {
                result.Delivered++;
            }
        }

        return result;
    }

    public async Task<string?> GetLabelBlockReasonAsync(string inpostShipmentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(inpostShipmentId))
        {
            return null;
        }

        var shipment = _shipmentRoRepo.GetData(x => x.InpostShipmentId == inpostShipmentId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefault();
        if (shipment == null)
        {
            return null;
        }

        if (InpostShipmentStatuses.IsDelivered(shipment.Status))
        {
            return DeliveredMessage(shipment.TrackingNumber, inpostShipmentId);
        }

        // Świeżo nadana przesyłka nie mogła jeszcze dotrzeć, a stan końcowy już się nie zmieni -
        // w obu przypadkach nie ma o co pytać ShipX.
        if (InpostShipmentStatuses.IsFinal(shipment.Status) ||
            shipment.Created > Subtract(DateTime.Now, _settings.MinShipmentAge))
        {
            return null;
        }

        // Sprawdzanie w tle może być do godziny do tyłu - o doręczeniu rozstrzyga stan z tej chwili.
        var settings = _inpostIntegrationSettingsService.GetShipXSettingsOrNull();
        if (settings == null)
        {
            return null;
        }

        var state = await _inpostShipXClient.GetShipmentAsync(settings, inpostShipmentId, ct);
        if (!state.Success)
        {
            _logger.LogWarning(
                "Przed wydrukiem etykiety nie udało się sprawdzić stanu przesyłki InPost {ShipmentId}: {Error}",
                inpostShipmentId, state.ErrorMessage);
            return null;
        }

        SaveState(shipment, state);
        if (!InpostShipmentStatuses.IsDelivered(state.Status))
        {
            return null;
        }

        return DeliveredMessage(
            string.IsNullOrWhiteSpace(state.TrackingNumber) ? shipment.TrackingNumber : state.TrackingNumber,
            inpostShipmentId);
    }

    /// <summary>
    /// Przesyłki do sprawdzenia: utworzone w ShipX, starsze niż <see cref="InpostDeliveryTrackingSettings.MinShipmentAge"/>,
    /// bez statusu końcowego. Przesyłki bez numeru listu też - ShipX mógł dokończyć opłatę już po
    /// czasie, który aplikacja odczekała przy tworzeniu.
    /// </summary>
    private List<InpostShipment> GetShipmentsAwaitingCheck(DateTime now)
    {
        var createdBefore = Subtract(now, _settings.MinShipmentAge);

        // Warunek na status to samo, co InpostShipmentStatuses.IsFinal - w postaci, którą EF
        // przełoży na SQL (przesyłki w statusie końcowym to większość historii).
        var shipments = _shipmentRoRepo.GetData(x =>
            x.InpostShipmentId != null && x.InpostShipmentId != "" &&
            x.Created <= createdBefore &&
            (x.Status == null ||
             (x.Status != InpostShipmentStatuses.Delivered &&
              x.Status != InpostShipmentStatuses.Canceled &&
              x.Status != InpostShipmentStatuses.Cancelled &&
              x.Status != InpostShipmentStatuses.ReturnedToSender)));

        // Limit wieku dopiero w pamięci: przesyłek, które utknęły bez statusu końcowego, jest garstka,
        // a zapytanie nie potrzebuje wtedy wartości zastępczej dla „bez limitu”.
        DateTime? createdAfter = _settings.MaxShipmentAge is { } maxAge ? Subtract(now, maxAge) : null;
        return shipments
            .Where(x => createdAfter == null || x.Created >= createdAfter)
            .OrderBy(x => x.Created)
            .ToList();
    }

    /// <summary>
    /// Zapisuje stan odczytany z ShipX, o ile coś się zmieniło. Numeru listu nigdy nie kasuje; gdy
    /// numer jest, zdejmuje zapisany wcześniej powód braku opłaty - przesyłka jest już opłacona.
    /// </summary>
    private bool SaveState(InpostShipment shipment, InpostShipmentResult state)
    {
        var status = string.IsNullOrWhiteSpace(state.Status) ? shipment.Status : state.Status;
        var trackingNumber = string.IsNullOrWhiteSpace(state.TrackingNumber)
            ? shipment.TrackingNumber
            : state.TrackingNumber;
        var errorMessage = string.IsNullOrWhiteSpace(trackingNumber) ? shipment.ErrorMessage : null;

        if (status == shipment.Status && trackingNumber == shipment.TrackingNumber &&
            errorMessage == shipment.ErrorMessage)
        {
            return false;
        }

        var id = shipment.Id;
        _shipmentWoRepo.UpdateData(x => x.Id == id, x =>
        {
            x.Status = status;
            x.TrackingNumber = trackingNumber;
            x.ErrorMessage = errorMessage;
        });

        if (InpostShipmentStatuses.IsDelivered(status) && !InpostShipmentStatuses.IsDelivered(shipment.Status))
        {
            _logger.LogInformation("Przesyłka InPost {TrackingNumber} ({Reference}) została doręczona",
                trackingNumber ?? shipment.InpostShipmentId, shipment.Reference);
        }

        return true;
    }

    private static string DeliveredMessage(string? trackingNumber, string inpostShipmentId)
    {
        var number = string.IsNullOrWhiteSpace(trackingNumber) ? inpostShipmentId : trackingNumber;
        return $"Przesyłka {number} została już doręczona - etykiety doręczonej przesyłki nie drukujemy ponownie.";
    }

    /// <summary>Odejmuje czas od daty bez wyjątku przy przesadnie dużych wartościach z ustawień.</summary>
    private static DateTime Subtract(DateTime date, TimeSpan age)
    {
        return age >= date - DateTime.MinValue ? DateTime.MinValue : date - age;
    }
}
