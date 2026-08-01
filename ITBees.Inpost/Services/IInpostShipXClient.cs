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
    /// Czeka (odpytując API) aż przesyłka otrzyma numer listu przewozowego,
    /// maksymalnie przez podany czas. Zwraca ostatni znany stan przesyłki.
    /// </summary>
    Task<InpostShipmentResult> WaitForTrackingNumberAsync(InpostSettings settings, string shipmentId,
        TimeSpan timeout, CancellationToken ct = default);

    /// <summary>Pobiera etykietę przesyłki (PDF) lub null, gdy jeszcze niedostępna.</summary>
    Task<byte[]?> GetLabelAsync(InpostSettings settings, string shipmentId, CancellationToken ct = default);
}
