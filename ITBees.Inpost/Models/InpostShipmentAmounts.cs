using System.Globalization;

namespace ITBees.Inpost.Models;

/// <summary>
/// Kwoty usług dodatkowych ShipX: ubezpieczenie (<c>insurance</c>) i pobranie (<c>cod</c>).
/// Waluta jest zawsze PLN. Limity są InPostu, nie nasze: przesyłkę za pobraniem ShipX przyjmuje
/// tylko ubezpieczoną co najmniej na kwotę pobrania ("Insurance should be equal or higher than COD").
/// </summary>
public static class InpostShipmentAmounts
{
    public const string Currency = "PLN";

    /// <summary>Najwyższa kwota ubezpieczenia przesyłki.</summary>
    public const decimal MaxInsurance = 20_000m;

    /// <summary>Najwyższa kwota pobrania.</summary>
    public const decimal MaxCod = 5_000m;

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    /// <summary>
    /// Sprawdza kwoty usług dodatkowych. Zwraca opis pierwszego błędu albo null, gdy wszystko gra.
    /// Null w obu kwotach znaczy: bez usług dodatkowych.
    /// </summary>
    public static string? Validate(decimal? insuranceAmount, decimal? codAmount)
    {
        if (insuranceAmount is <= 0)
            return "Kwota ubezpieczenia musi być większa od zera.";
        if (insuranceAmount > MaxInsurance)
            return $"Kwota ubezpieczenia nie może przekraczać {Format(MaxInsurance)}.";
        if (codAmount is <= 0)
            return "Kwota pobrania musi być większa od zera.";
        if (codAmount > MaxCod)
            return $"Kwota pobrania nie może przekraczać {Format(MaxCod)}.";
        if (codAmount.HasValue && (!insuranceAmount.HasValue || insuranceAmount < codAmount))
            return "Przesyłka za pobraniem musi być ubezpieczona co najmniej na kwotę pobrania.";

        return null;
    }

    public static string Format(decimal amount) => amount.ToString("N2", Polish) + " " + Currency;
}
