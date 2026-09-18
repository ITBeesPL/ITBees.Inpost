using ITBees.Inpost.Services;

namespace ITBees.Inpost;

/// <summary>
/// Sprawdzanie, czy nadane przesyłki zostały już doręczone (<see cref="IInpostDeliveryTrackingService"/>).
/// Przekazywane do <see cref="Setup.InpostSetup.Register"/>; bez nich obowiązują wartości domyślne.
/// </summary>
public class InpostDeliveryTrackingSettings
{
    /// <summary>
    /// Gdy false, przesyłki nie są sprawdzane w tle. Blokada wydruku etykiety doręczonej przesyłki
    /// działa nadal (przed wydrukiem status starszej przesyłki jest sprawdzany w ShipX na bieżąco).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Co ile uruchamiane jest sprawdzanie w tle.</summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Przesyłki młodsze niż ten czas nie są sprawdzane - świeżo nadana paczka nie mogła jeszcze
    /// dotrzeć. Od tego samego progu przed wydrukiem etykiety pytamy ShipX o bieżący status.
    /// </summary>
    public TimeSpan MinShipmentAge { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Przesyłki starsze niż ten czas nie są już sprawdzane w tle: paczka, która przez tyle czasu
    /// nie doszła ani nie wróciła, utknęła (np. opłacona, ale nigdy nienadana) i odpytywanie jej co
    /// godzinę nic nie da. Null - bez limitu. Blokada wydruku sprawdza takie przesyłki nadal.
    /// </summary>
    public TimeSpan? MaxShipmentAge { get; set; } = TimeSpan.FromDays(60);

    /// <summary>Odstęp między kolejnymi zapytaniami do ShipX w jednym przebiegu - nie obciąża limitów API.</summary>
    public TimeSpan DelayBetweenRequests { get; set; } = TimeSpan.FromMilliseconds(500);
}
