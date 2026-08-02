using ITBees.Inpost.Models;
using ITBees.Interfaces.Repository;
using ITBees.RestfulApiControllers.Models;

namespace ITBees.Inpost.Services;

/// <summary>
/// Tworzenie przesyłek ShipX wraz z zapisem historii w bazie aplikacji hosta
/// oraz obsługa listy przesyłek (statusy, numery listów przewozowych).
/// </summary>
public interface IInpostShipmentRecordsService
{
    /// <summary>
    /// Tworzy przesyłkę w ShipX i zapisuje jej rekord w bazie. Błędy API InPost nie są rzucane -
    /// wracają w polach ErrorMessage/Success, a rekord z błędem również trafia do historii.
    /// Rzuca FasApiErrorException, gdy integracja nie jest skonfigurowana.
    /// </summary>
    InpostShipmentVm CreateShipment(CreateShipmentOrder order, Guid? externalGuid = null, int? externalId = null,
        Guid? createdByGuid = null);

    PaginatedResult<InpostShipmentRecordVm> GetPaginated(string? search, int? page, int? pageSize,
        string? sortColumn, SortOrder? sortOrder);

    /// <summary>Odpytuje ShipX o aktualny stan przesyłki, zapisuje go i zwraca zaktualizowany rekord.</summary>
    InpostShipmentRecordVm RefreshStatus(int id);
}
