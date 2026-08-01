namespace ITBees.Inpost.Entities;

/// <summary>
/// Ustawienia integracji z InPost (ShipX) przechowywane w bazie aplikacji hosta -
/// konfigurowane w panelu administracyjnym.
/// </summary>
public class InpostIntegrationSettings
{
    public int Id { get; set; }

    /// <summary>Token API ShipX wygenerowany w Menedżerze Paczek InPost.</summary>
    public string ApiToken { get; set; } = "";

    /// <summary>Identyfikator organizacji w ShipX.</summary>
    public string OrganizationId { get; set; } = "";

    /// <summary>Gdy true, przesyłki tworzone są w środowisku testowym (sandbox).</summary>
    public bool UseSandbox { get; set; }

    public DateTime? Modified { get; set; }
    public Guid? ModifiedByGuid { get; set; }
}
