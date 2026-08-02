using ITBees.Inpost.Entities;

namespace ITBees.Inpost.Models;

/// <summary>Rekord listy przesyłek InPost prezentowany w panelu administracyjnym.</summary>
public class InpostShipmentRecordVm
{
    public InpostShipmentRecordVm()
    {
    }

    public InpostShipmentRecordVm(InpostShipment x)
    {
        Id = x.Id;
        Created = x.Created;
        Reference = x.Reference;
        ReceiverName = x.ReceiverName;
        ShipmentType = x.ShipmentType;
        ParcelTemplate = x.ParcelTemplate;
        TargetPoint = x.TargetPoint;
        InpostShipmentId = x.InpostShipmentId;
        TrackingNumber = x.TrackingNumber;
        Status = x.Status;
        ErrorMessage = x.ErrorMessage;
    }

    public int Id { get; set; }
    public DateTime Created { get; set; }
    public string? Reference { get; set; }
    public string? ReceiverName { get; set; }
    public string ShipmentType { get; set; } = "";
    public string ParcelTemplate { get; set; } = "";
    public string? TargetPoint { get; set; }
    public string? InpostShipmentId { get; set; }
    public string? TrackingNumber { get; set; }
    public string? Status { get; set; }
    public string? ErrorMessage { get; set; }
}
