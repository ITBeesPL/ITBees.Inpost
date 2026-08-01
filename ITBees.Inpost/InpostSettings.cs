namespace ITBees.Inpost;

public class InpostSettings
{
    public const string ProductionBaseUrl = "https://api-shipx-pl.easypack24.net";
    public const string SandboxBaseUrl = "https://sandbox-api-shipx-pl.easypack24.net";

    /// <summary>
    /// Adres bazowy API ShipX. Domyślnie środowisko produkcyjne,
    /// dla testów użyj <see cref="SandboxBaseUrl"/>.
    /// </summary>
    public string BaseUrl { get; set; } = ProductionBaseUrl;

    /// <summary>
    /// Token API ShipX (Bearer) wygenerowany w Menedżerze Paczek InPost.
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// Identyfikator organizacji w ShipX - wymagany do tworzenia przesyłek.
    /// </summary>
    public string OrganizationId { get; set; } = "";
}
