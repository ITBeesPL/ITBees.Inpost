namespace ITBees.Inpost.Models;

/// <summary>
/// Sposoby nadania przesyłki (<c>custom_attributes.sending_method</c>) - mówią InPostowi,
/// jak paczka trafi do sieci. ShipX wymaga tego pola zarówno dla przesyłek paczkomatowych,
/// jak i dla kuriera C2C.
/// </summary>
public static class InpostSendingMethods
{
    /// <summary>Nadanie w Paczkomacie.</summary>
    public const string ParcelLocker = "parcel_locker";

    /// <summary>Odbiór przez kuriera (samo pole deklaruje zamiar - zlecenie odbioru zamawia się osobno).</summary>
    public const string DispatchOrder = "dispatch_order";

    /// <summary>Nadanie w PaczkoPunkcie.</summary>
    public const string Pop = "pop";

    /// <summary>Nadanie w Punkcie Obsługi Klienta.</summary>
    public const string Pok = "pok";

    /// <summary>Nadanie w Punkcie Obsługi Klienta - wariant kurierski.</summary>
    public const string CourierPok = "courier_pok";

    /// <summary>Nadanie w oddziale InPost.</summary>
    public const string Branch = "branch";

    public static readonly string[] All =
        { ParcelLocker, DispatchOrder, Pop, Pok, CourierPok, Branch };

    /// <summary>
    /// Sposób nadania używany, gdy wywołujący go nie poda. Paczkę paczkomatową domyślnie
    /// zanosi się do Paczkomatu, a kurierską odbiera kurier - gabaryt D i tak nie zmieści się
    /// w skrytce, więc odbiór jest jedynym sposobem działającym dla wszystkich gabarytów.
    /// </summary>
    public static string DefaultFor(InpostShipmentType shipmentType) =>
        shipmentType == InpostShipmentType.Courier ? DispatchOrder : ParcelLocker;

    public static bool IsKnown(string? sendingMethod) =>
        sendingMethod != null && All.Contains(sendingMethod);
}
