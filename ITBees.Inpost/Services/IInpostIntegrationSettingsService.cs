using ITBees.Inpost.Models;

namespace ITBees.Inpost.Services;

public interface IInpostIntegrationSettingsService
{
    InpostIntegrationSettingsVm Get();

    InpostIntegrationSettingsVm Update(InpostIntegrationSettingsIm im, Guid? modifiedByGuid = null);

    /// <summary>
    /// Zwraca ustawienia ShipX gotowe do użycia z <see cref="IInpostShipXClient"/>
    /// (BaseUrl dobrany wg flagi sandbox) albo null, gdy integracja nie jest skonfigurowana.
    /// </summary>
    InpostSettings? GetShipXSettingsOrNull();
}
