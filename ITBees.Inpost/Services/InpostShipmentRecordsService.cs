using ITBees.Inpost.Entities;
using ITBees.Inpost.Models;
using ITBees.Interfaces.Repository;
using ITBees.RestfulApiControllers.Exceptions;
using ITBees.RestfulApiControllers.Models;
using Microsoft.Extensions.Logging;

namespace ITBees.Inpost.Services;

public class InpostShipmentRecordsService : IInpostShipmentRecordsService
{
    private readonly IInpostShipXClient _inpostShipXClient;
    private readonly IInpostIntegrationSettingsService _inpostIntegrationSettingsService;
    private readonly IReadOnlyRepository<InpostShipment> _shipmentRoRepo;
    private readonly IWriteOnlyRepository<InpostShipment> _shipmentWoRepo;
    private readonly ILogger<InpostShipmentRecordsService> _logger;

    public InpostShipmentRecordsService(
        IInpostShipXClient inpostShipXClient,
        IInpostIntegrationSettingsService inpostIntegrationSettingsService,
        IReadOnlyRepository<InpostShipment> shipmentRoRepo,
        IWriteOnlyRepository<InpostShipment> shipmentWoRepo,
        ILogger<InpostShipmentRecordsService> logger)
    {
        _inpostShipXClient = inpostShipXClient;
        _inpostIntegrationSettingsService = inpostIntegrationSettingsService;
        _shipmentRoRepo = shipmentRoRepo;
        _shipmentWoRepo = shipmentWoRepo;
        _logger = logger;
    }

    public InpostShipmentVm CreateShipment(CreateShipmentOrder order, Guid? externalGuid = null,
        int? externalId = null, Guid? createdByGuid = null)
    {
        var settings = GetSettingsOrThrow();
        var shipmentType = InpostShipmentTypes.FromShipmentType(order.ShipmentType);
        order.TargetPoint = order.TargetPoint?.Trim().ToUpperInvariant();

        var result = _inpostShipXClient.CreateShipmentAsync(settings, order).GetAwaiter().GetResult();

        if (result.Success && !string.IsNullOrEmpty(result.ShipmentId) &&
            string.IsNullOrEmpty(result.TrackingNumber))
        {
            // Numer listu przewozowego nadawany jest asynchronicznie po zakupie oferty.
            result = _inpostShipXClient
                .WaitForTrackingNumberAsync(settings, result.ShipmentId, TimeSpan.FromSeconds(45))
                .GetAwaiter().GetResult();
        }

        if (!result.Success)
        {
            _logger.LogError("Nie udało się utworzyć przesyłki InPost ({Reference}): {Error}",
                order.Reference, result.ErrorMessage);
        }

        _shipmentWoRepo.InsertData(new InpostShipment()
        {
            Created = DateTime.Now,
            CreatedByGuid = createdByGuid,
            ExternalGuid = externalGuid,
            ExternalId = externalId,
            Reference = order.Reference,
            ReceiverName = order.ReceiverCompanyName ??
                           $"{order.ReceiverFirstName} {order.ReceiverLastName}".Trim(),
            ShipmentType = shipmentType,
            ParcelTemplate = order.ParcelTemplate,
            TargetPoint = order.TargetPoint,
            InpostShipmentId = result.ShipmentId,
            TrackingNumber = result.TrackingNumber,
            Status = result.Status,
            ErrorMessage = result.ErrorMessage
        });

        return new InpostShipmentVm(result, shipmentType, order.ParcelTemplate, order.TargetPoint);
    }

    public PaginatedResult<InpostShipmentRecordVm> GetPaginated(string? search, int? page, int? pageSize,
        string? sortColumn, SortOrder? sortOrder)
    {
        var sortOptions = new SortOptions(page, pageSize, sortColumn ?? "Created",
            sortOrder ?? SortOrder.Descending);

        if (string.IsNullOrEmpty(search))
        {
            return _shipmentRoRepo
                .GetDataPaginated(x => true, sortOptions)
                .MapTo(x => new InpostShipmentRecordVm(x));
        }

        search = search.ToLower();
        return _shipmentRoRepo
            .GetDataPaginated(x => x.Reference.ToLower().Contains(search) ||
                                   x.ReceiverName.ToLower().Contains(search) ||
                                   x.TrackingNumber.ToLower().Contains(search) ||
                                   x.TargetPoint.ToLower().Contains(search) ||
                                   x.Status.ToLower().Contains(search),
                sortOptions)
            .MapTo(x => new InpostShipmentRecordVm(x));
    }

    public InpostShipmentRecordVm RefreshStatus(int id)
    {
        var shipment = _shipmentRoRepo.GetData(x => x.Id == id).FirstOrDefault();
        if (shipment == null)
        {
            throw new FasApiErrorException("Nie znaleziono przesyłki o podanym identyfikatorze.", 404);
        }

        if (string.IsNullOrEmpty(shipment.InpostShipmentId))
        {
            throw new FasApiErrorException(
                "Ta przesyłka nie została utworzona w InPost (brak identyfikatora ShipX) - nie ma czego odświeżyć.",
                400);
        }

        var settings = GetSettingsOrThrow();
        var result = _inpostShipXClient.GetShipmentAsync(settings, shipment.InpostShipmentId)
            .GetAwaiter().GetResult();

        if (!result.Success)
        {
            throw new FasApiErrorException(
                result.ErrorMessage ?? "Nie udało się pobrać stanu przesyłki z InPost.", 502);
        }

        if (string.IsNullOrEmpty(result.TrackingNumber))
        {
            // Odświeżenie dokańcza też przesyłki, które utknęły przed zakupem oferty
            // (np. gdy ShipX przygotowywał oferty dłużej niż trwało tworzenie urządzenia).
            result = _inpostShipXClient
                .WaitForTrackingNumberAsync(settings, shipment.InpostShipmentId, TimeSpan.FromSeconds(30))
                .GetAwaiter().GetResult();
        }

        var updated = _shipmentWoRepo.UpdateData(x => x.Id == id, x =>
        {
            x.Status = result.Status;
            x.TrackingNumber = string.IsNullOrEmpty(result.TrackingNumber)
                ? x.TrackingNumber
                : result.TrackingNumber;
            // Powód, dla którego przesyłka nie dostała jeszcze numeru listu (np. nieudany zakup oferty),
            // trafia na listę - inaczej operator widzi tylko pusty status.
            x.ErrorMessage = string.IsNullOrEmpty(result.TrackingNumber) ? result.ErrorMessage : null;
        }).First();

        return new InpostShipmentRecordVm(updated);
    }

    public void Delete(int id)
    {
        var shipment = _shipmentRoRepo.GetData(x => x.Id == id).FirstOrDefault();
        if (shipment == null)
        {
            throw new FasApiErrorException("Nie znaleziono przesyłki o podanym identyfikatorze.", 404);
        }

        if (!string.IsNullOrWhiteSpace(shipment.TrackingNumber))
        {
            throw new FasApiErrorException(
                $"Przesyłka ma już nadany numer listu przewozowego ({shipment.TrackingNumber}) - " +
                "nie można usunąć jej z historii. Anuluj ją najpierw w Menedżerze Paczek InPost.", 400);
        }

        _shipmentWoRepo.DeleteData(x => x.Id == id);
    }

    public string GetRawShipmentJson(int id)
    {
        var shipment = _shipmentRoRepo.GetData(x => x.Id == id).FirstOrDefault();
        if (shipment == null)
        {
            throw new FasApiErrorException("Nie znaleziono przesyłki o podanym identyfikatorze.", 404);
        }

        if (string.IsNullOrEmpty(shipment.InpostShipmentId))
        {
            throw new FasApiErrorException(
                "Ta przesyłka nie została utworzona w InPost (brak identyfikatora ShipX).", 400);
        }

        var settings = GetSettingsOrThrow();
        var result = _inpostShipXClient.GetShipmentAsync(settings, shipment.InpostShipmentId)
            .GetAwaiter().GetResult();

        return result.RawJson ?? result.ErrorMessage ?? "";
    }

    private InpostSettings GetSettingsOrThrow()
    {
        var settings = _inpostIntegrationSettingsService.GetShipXSettingsOrNull();
        if (settings == null)
        {
            throw new FasApiErrorException(
                "Integracja z InPost nie jest skonfigurowana. Uzupełnij klucz API w zakładce Integracje.", 400);
        }

        return settings;
    }
}
