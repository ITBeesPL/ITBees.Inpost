namespace ITBees.Inpost.Models;

/// <summary>
/// Typy etykiet ShipX (parametr „type” pobierania etykiety). Wartości są przekazywane do API
/// InPost dokładnie w tej pisowni.
/// </summary>
public static class InpostLabelTypes
{
    /// <summary>Domyślna etykieta na stronie A4 - do zwykłej drukarki biurowej.</summary>
    public const string Normal = "normal";

    /// <summary>Pojedyncza etykieta A6 (105 × 148 mm) - do drukarki etykiet.</summary>
    public const string A6 = "A6";

    /// <summary>
    /// Sprowadza wartość z zapytania do pisowni ShipX; null dla pustej lub nieznanej wartości
    /// (wtedy obowiązuje etykieta domyślna).
    /// </summary>
    public static string? Normalize(string? labelType)
    {
        var value = (labelType ?? string.Empty).Trim();
        if (value.Equals(A6, StringComparison.OrdinalIgnoreCase))
        {
            return A6;
        }

        return value.Equals(Normal, StringComparison.OrdinalIgnoreCase) ? Normal : null;
    }
}
