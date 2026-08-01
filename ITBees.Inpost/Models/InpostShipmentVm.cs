namespace ITBees.Inpost.Models;

/// <summary>
/// Wynik utworzenia przesyłki InPost zwracany do panelu administracyjnego.
/// </summary>
public class InpostShipmentVm
{
    public InpostShipmentVm()
    {
    }

    public InpostShipmentVm(InpostShipmentResult result, string shipmentType, string parcelTemplate,
        string? targetPoint)
    {
        Success = result.Success;
        ShipmentType = shipmentType;
        ParcelTemplate = parcelTemplate;
        TargetPoint = targetPoint;
        InpostShipmentId = result.ShipmentId;
        TrackingNumber = result.TrackingNumber;
        Status = result.Status;
        ErrorMessage = result.ErrorMessage;
    }

    public bool Success { get; set; }

    /// <summary>locker (paczkomat) lub courier (kurier InPost).</summary>
    public string ShipmentType { get; set; } = "";

    /// <summary>Gabaryt przesyłki (small/medium/large/xlarge).</summary>
    public string ParcelTemplate { get; set; } = "";

    public string? TargetPoint { get; set; }

    /// <summary>Identyfikator przesyłki w ShipX.</summary>
    public string? InpostShipmentId { get; set; }

    public string? TrackingNumber { get; set; }
    public string? Status { get; set; }
    public string? ErrorMessage { get; set; }
}
