namespace ITBees.Inpost.Models;

/// <summary>
/// Dane potrzebne do utworzenia przesyłki (listu przewozowego) w InPost ShipX.
/// </summary>
public class CreateShipmentOrder
{
    public InpostShipmentType ShipmentType { get; set; } = InpostShipmentType.ParcelLocker;

    /// <summary>Gabaryt przesyłki - patrz <see cref="InpostParcelTemplates"/>.</summary>
    public string ParcelTemplate { get; set; } = InpostParcelTemplates.Small;

    /// <summary>
    /// Kod paczkomatu docelowego (np. KRA012). Wymagany dla przesyłek paczkomatowych.
    /// </summary>
    public string? TargetPoint { get; set; }

    /// <summary>Nazwa firmy odbiorcy (lub imię i nazwisko w polach poniżej).</summary>
    public string? ReceiverCompanyName { get; set; }

    public string? ReceiverFirstName { get; set; }
    public string? ReceiverLastName { get; set; }
    public string ReceiverEmail { get; set; } = "";
    public string ReceiverPhone { get; set; } = "";

    // Adres odbiorcy - wymagany dla przesyłek kurierskich.
    public string? ReceiverStreet { get; set; }
    public string? ReceiverBuildingNumber { get; set; }
    public string? ReceiverCity { get; set; }
    public string? ReceiverPostCode { get; set; }
    public string ReceiverCountryCode { get; set; } = "PL";

    /// <summary>Własne oznaczenie przesyłki (np. numer seryjny urządzenia).</summary>
    public string? Reference { get; set; }

    public string? Comments { get; set; }

    /// <summary>
    /// Sposób nadania przesyłki paczkomatowej:
    /// parcel_locker (nadanie w paczkomacie) lub dispatch_order (odbiór przez kuriera).
    /// </summary>
    public string SendingMethod { get; set; } = "parcel_locker";
}
