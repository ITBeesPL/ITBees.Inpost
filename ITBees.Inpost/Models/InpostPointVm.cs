namespace ITBees.Inpost.Models;

/// <summary>Punkt InPost (paczkomat / PoP) zwracany z wyszukiwarki punktów ShipX.</summary>
public class InpostPointVm
{
    /// <summary>Kod punktu, np. KRA012 - wysyłany do ShipX jako target_point.</summary>
    public string Name { get; set; } = "";

    public string? Street { get; set; }
    public string? City { get; set; }
    public string? PostCode { get; set; }

    /// <summary>Opis lokalizacji, np. "przy sklepie Żabka".</summary>
    public string? LocationDescription { get; set; }

    /// <summary>Gotowy do wyświetlenia adres punktu.</summary>
    public string AddressText =>
        string.Join(", ", new[] { Street, string.Join(" ", new[] { PostCode, City }
                .Where(x => !string.IsNullOrWhiteSpace(x))) }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
}
