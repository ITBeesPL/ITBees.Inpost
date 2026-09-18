namespace ITBees.Inpost.Models;

/// <summary>Wynik jednego przebiegu sprawdzania doręczeń (do logów).</summary>
public class InpostDeliveryCheckResult
{
    /// <summary>Integracja nie jest skonfigurowana - niczego nie sprawdzano.</summary>
    public bool NotConfigured { get; set; }

    /// <summary>Ile przesyłek czekało na sprawdzenie.</summary>
    public int Pending { get; set; }

    /// <summary>O ile przesyłek zapytano ShipX.</summary>
    public int Checked { get; set; }

    /// <summary>Ilu przesyłkom zmienił się zapisany stan (status, numer listu).</summary>
    public int Updated { get; set; }

    /// <summary>Ile przesyłek okazało się doręczonych.</summary>
    public int Delivered { get; set; }

    /// <summary>Ile zapytań do ShipX się nie udało.</summary>
    public int Failed { get; set; }

    /// <summary>
    /// Przebieg przerwany po kilku błędach z rzędu (ShipX nieosiągalny albo nieważny token) -
    /// reszta przesyłek zostanie sprawdzona w następnym przebiegu.
    /// </summary>
    public bool Aborted { get; set; }

    /// <summary>Treść ostatniego błędu ShipX.</summary>
    public string? LastError { get; set; }
}
