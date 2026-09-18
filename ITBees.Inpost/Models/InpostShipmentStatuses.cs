namespace ITBees.Inpost.Models;

/// <summary>
/// Statusy przesyłki ShipX, od których zależy obsługa przesyłki po nadaniu: etykiety doręczonej
/// przesyłki nie drukujemy ponownie, a przesyłek w statusie końcowym nie sprawdzamy już w tle.
/// </summary>
public static class InpostShipmentStatuses
{
    /// <summary>Przesyłka doręczona - odebrana z Paczkomatu albo doręczona przez kuriera.</summary>
    public const string Delivered = "delivered";

    /// <summary>Etykieta anulowana (np. w Menedżerze Paczek InPost).</summary>
    public const string Canceled = "canceled";

    /// <summary>Pisownia brytyjska - ShipX używa „canceled”, ale nie zakładamy, że zawsze.</summary>
    public const string Cancelled = "cancelled";

    /// <summary>Przesyłka zwrócona do nadawcy.</summary>
    public const string ReturnedToSender = "returned_to_sender";

    public static bool IsDelivered(string? status) =>
        string.Equals(status?.Trim(), Delivered, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Status, po którym stan przesyłki już się nie zmieni (doręczona, anulowana, zwrócona) -
    /// takiej przesyłki nie ma sensu dalej sprawdzać.
    /// </summary>
    public static bool IsFinal(string? status)
    {
        var value = status?.Trim();
        return string.Equals(value, Delivered, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Canceled, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Cancelled, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, ReturnedToSender, StringComparison.OrdinalIgnoreCase);
    }
}
