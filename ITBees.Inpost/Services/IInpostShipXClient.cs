using ITBees.Inpost.Models;

namespace ITBees.Inpost.Services;

public interface IInpostShipXClient
{
    /// <summary>
    /// Tworzy przesyłkę (list przewozowy) w InPost ShipX w trybie uproszczonym -
    /// oferta jest automatycznie kupowana przez API.
    /// </summary>
    Task<InpostShipmentResult> CreateShipmentAsync(InpostSettings settings, CreateShipmentOrder order,
        CancellationToken ct = default);

    /// <summary>Pobiera aktualny stan przesyłki (m.in. numer listu przewozowego).</summary>
    Task<InpostShipmentResult> GetShipmentAsync(InpostSettings settings, string shipmentId,
        CancellationToken ct = default);

    /// <summary>
    /// Kupuje wybraną ofertę przewozu. Bez tego kroku przesyłka pozostaje w statusie
    /// offer_selected - nie dostaje numeru listu przewozowego ani etykiety.
    /// </summary>
    Task<InpostShipmentResult> BuyShipmentOfferAsync(InpostSettings settings, string shipmentId,
        long? offerId = null, CancellationToken ct = default);

    /// <summary>
    /// Czeka (odpytując API) aż przesyłka otrzyma numer listu przewozowego,
    /// maksymalnie przez podany czas. Zwraca ostatni znany stan przesyłki.
    /// </summary>
    Task<InpostShipmentResult> WaitForTrackingNumberAsync(InpostSettings settings, string shipmentId,
        TimeSpan timeout, CancellationToken ct = default);

    /// <summary>Pobiera etykietę przesyłki (PDF) lub null, gdy jeszcze niedostępna.</summary>
    Task<byte[]?> GetLabelAsync(InpostSettings settings, string shipmentId, CancellationToken ct = default);

    /// <summary>
    /// Pobiera etykietę wraz z powodem niedostępności zwróconym przez ShipX -
    /// pozwala pokazać operatorowi konkretną przyczynę zamiast ogólnego komunikatu.
    /// </summary>
    Task<InpostLabelResult> GetLabelWithDetailsAsync(InpostSettings settings, string shipmentId,
        CancellationToken ct = default);

    /// <summary>
    /// Wyszukuje czynne paczkomaty po kodzie punktu, mieście lub kodzie pocztowym -
    /// pozwala wybrać paczkomat z listy zamiast wpisywać kod ręcznie.
    /// </summary>
    Task<List<InpostPointVm>> SearchParcelLockersAsync(InpostSettings settings, string? search, int limit = 25,
        CancellationToken ct = default);
}
