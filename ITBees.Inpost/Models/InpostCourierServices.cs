namespace ITBees.Inpost.Models;

/// <summary>
/// Nazwy usług kurierskich ShipX.
///
/// InPost udostępnia kurierowi dwie osobne usługi i o tym, która z nich zadziała,
/// decyduje rodzaj konta w Menedżerze Paczek:
/// <list type="bullet">
/// <item><description><see cref="Standard"/> - dla organizacji z podpisaną umową kurierską (postpaid).</description></item>
/// <item><description><see cref="C2C"/> - dla kont przedpłaconych, bez umowy.</description></item>
/// </list>
/// Użycie usługi z umową na koncie przedpłaconym kończy się błędem
/// <c>trucker_ID_is_not_set_for_organization</c>.
/// </summary>
public static class InpostCourierServices
{
    /// <summary>Kurier InPost dla organizacji z umową kurierską.</summary>
    public const string Standard = "inpost_courier_standard";

    /// <summary>Kurier InPost C2C - usługa kont przedpłaconych (bez umowy).</summary>
    public const string C2C = "inpost_courier_c2c";

    /// <summary>Usługa paczkomatowa - ta sama dla kont z umową i przedpłaconych.</summary>
    public const string LockerStandard = "inpost_locker_standard";
}

/// <summary>
/// Sposób wyboru usługi kurierskiej dla organizacji.
/// </summary>
public enum InpostCourierServiceMode
{
    /// <summary>
    /// Próbuje nadać przesyłkę usługą z umową, a gdy organizacja umowy nie ma -
    /// powtarza próbę usługą C2C. Domyślne zachowanie, działa na obu rodzajach kont.
    /// </summary>
    Auto = 0,

    /// <summary>Zawsze <see cref="InpostCourierServices.Standard"/>.</summary>
    Standard = 1,

    /// <summary>Zawsze <see cref="InpostCourierServices.C2C"/>.</summary>
    C2C = 2
}
