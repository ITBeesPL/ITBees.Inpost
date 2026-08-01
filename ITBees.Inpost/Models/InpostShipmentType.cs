namespace ITBees.Inpost.Models;

public enum InpostShipmentType
{
    /// <summary>Przesyłka do paczkomatu (inpost_locker_standard).</summary>
    ParcelLocker = 0,

    /// <summary>Przesyłka kurierska InPost (inpost_courier_standard).</summary>
    Courier = 1
}
