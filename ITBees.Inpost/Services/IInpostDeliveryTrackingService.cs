using ITBees.Inpost.Models;

namespace ITBees.Inpost.Services;

/// <summary>
/// Sprawdzanie, czy nadane przesyłki zostały już doręczone. Etykiety doręczonej przesyłki nie
/// drukujemy ponownie - stąd zarówno sprawdzanie w tle (lista przesyłek wie, komu ukryć wydruk),
/// jak i sprawdzenie w chwili wydruku (tło może być do godziny do tyłu).
/// Stan przesyłki jest wyłącznie odczytywany z ShipX - nic nie jest kupowane ani anulowane.
/// </summary>
public interface IInpostDeliveryTrackingService
{
    /// <summary>
    /// Jeden przebieg sprawdzania w tle: pyta ShipX o przesyłki starsze niż
    /// <see cref="InpostDeliveryTrackingSettings.MinShipmentAge"/> (i nie starsze niż
    /// <see cref="InpostDeliveryTrackingSettings.MaxShipmentAge"/>), które nie mają jeszcze statusu
    /// końcowego (<see cref="InpostShipmentStatuses.IsFinal"/>), i zapisuje zmiany ich stanu.
    /// </summary>
    Task<InpostDeliveryCheckResult> CheckPendingShipmentsAsync(CancellationToken ct = default);

    /// <summary>
    /// Powód, dla którego etykiety przesyłki (identyfikator ShipX) nie wolno wydrukować, albo null,
    /// gdy wolno. Dziś jedynym powodem jest doręczenie. Przesyłkę starszą niż
    /// <see cref="InpostDeliveryTrackingSettings.MinShipmentAge"/>, która w bazie nie jest jeszcze
    /// doręczona, sprawdza w ShipX na bieżąco (i zapisuje wynik). Gdy ShipX nie odpowiada, wydruku
    /// nie blokuje; przesyłek spoza historii (utworzonych poza aplikacją) nie ocenia.
    /// </summary>
    Task<string?> GetLabelBlockReasonAsync(string inpostShipmentId, CancellationToken ct = default);
}
