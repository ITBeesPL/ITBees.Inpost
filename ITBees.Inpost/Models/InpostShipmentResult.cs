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

    public string? ErrorMessage { get; set; }

    /// <summary>Pełna odpowiedź API (do diagnostyki).</summary>
    public string? RawJson { get; set; }
}
