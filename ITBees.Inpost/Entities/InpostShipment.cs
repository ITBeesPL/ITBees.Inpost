namespace ITBees.Inpost.Entities;

/// <summary>
/// Zapis przesyłki (listu przewozowego) utworzonej w InPost ShipX -
/// historia przesyłek przechowywana w bazie aplikacji hosta.
/// </summary>
public class InpostShipment
{
    public int Id { get; set; }

    public DateTime Created { get; set; }
    public Guid? CreatedByGuid { get; set; }

    /// <summary>Guid obiektu aplikacji hosta powiązanego z przesyłką (np. urządzenia GPS).</summary>
    public Guid? ExternalGuid { get; set; }

    /// <summary>Identyfikator liczbowy obiektu aplikacji hosta (np. pozycji magazynowej).</summary>
    public int? ExternalId { get; set; }

    /// <summary>Własne oznaczenie przesyłki wysyłane do ShipX (np. numer seryjny urządzenia).</summary>
    public string? Reference { get; set; }

    /// <summary>Nazwa odbiorcy przesyłki (np. kontrahent).</summary>
    public string? ReceiverName { get; set; }

    /// <summary>locker (paczkomat) lub courier (kurier InPost) - patrz <see cref="Models.InpostShipmentTypes"/>.</summary>
    public string ShipmentType { get; set; } = "";

    /// <summary>Gabaryt przesyłki (small/medium/large/xlarge).</summary>
    public string ParcelTemplate { get; set; } = "";

    /// <summary>Kod paczkomatu docelowego (dla przesyłek paczkomatowych).</summary>
    public string? TargetPoint { get; set; }

    /// <summary>Identyfikator przesyłki w ShipX.</summary>
    public string? InpostShipmentId { get; set; }

    public string? TrackingNumber { get; set; }
    public string? Status { get; set; }
    public string? ErrorMessage { get; set; }
}
