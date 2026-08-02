namespace ITBees.Inpost.Models;

public class InpostShipmentResult
{
    public bool Success { get; set; }

    /// <summary>Identyfikator przesyłki w ShipX.</summary>
    public string? ShipmentId { get; set; }

    /// <summary>Numer listu przewozowego (może pojawić się dopiero po chwili od utworzenia).</summary>
    public string? TrackingNumber { get; set; }

    /// <summary>Status przesyłki w ShipX (np. created, confirmed, offer_selected).</summary>
    public string? Status { get; set; }

    /// <summary>
    /// Identyfikator oferty, którą można kupić (status available/selected).
    /// Null oznacza, że ShipX nie ma aktualnie kupowalnej oferty.
    /// </summary>
    public long? SelectedOfferId { get; set; }

    /// <summary>Status oferty wskazanej przez ShipX (available, selected, unavailable, expired).</summary>
    public string? SelectedOfferStatus { get; set; }

    /// <summary>Powody niedostępności ofert podane przez ShipX (unavailability_reasons).</summary>
    public string? OfferUnavailabilityReasons { get; set; }

    /// <summary>Status ostatniej transakcji rozliczeniowej (success/failure) - rozstrzyga o problemach z kontem.</summary>
    public string? LastTransactionStatus { get; set; }

    /// <summary>Rodzaj problemu - decyduje, czy ponawiać próbę i co pokazać operatorowi.</summary>
    public InpostErrorKind ErrorKind { get; set; } = InpostErrorKind.None;

    public string? ErrorMessage { get; set; }

    /// <summary>Pełna odpowiedź API (do diagnostyki).</summary>
    public string? RawJson { get; set; }
}
