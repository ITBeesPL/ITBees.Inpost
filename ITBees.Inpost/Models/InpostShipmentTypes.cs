namespace ITBees.Inpost.Models;

/// <summary>Tekstowe oznaczenia typu przesyłki używane w zapisach i API panelu.</summary>
public static class InpostShipmentTypes
{
    public const string Locker = "locker";
    public const string Courier = "courier";

    public static string FromShipmentType(InpostShipmentType shipmentType) =>
        shipmentType == InpostShipmentType.Courier ? Courier : Locker;
}
