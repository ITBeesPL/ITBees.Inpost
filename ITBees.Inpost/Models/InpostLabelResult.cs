namespace ITBees.Inpost.Models;

public class InpostLabelResult
{
    /// <summary>Zawartość etykiety (PDF) lub null, gdy nie udało się jej pobrać.</summary>
    public byte[]? Content { get; set; }

    /// <summary>Powód niedostępności etykiety zwrócony przez ShipX.</summary>
    public string? ErrorMessage { get; set; }
}
