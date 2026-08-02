using ITBees.Inpost.Models;
using ITBees.Inpost.Services;
using ITBees.Interfaces.Repository;
using ITBees.RestfulApiControllers;
using ITBees.RestfulApiControllers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Inpost.Controllers;

/// <summary>Lista utworzonych przesyłek InPost (listów przewozowych) dla panelu administracyjnego.</summary>
[Authorize(Roles = "PlatformOperator")]
public class InpostShipmentsController : RestfulControllerBase<InpostShipmentsController>
{
    private readonly IInpostShipmentRecordsService _inpostShipmentRecordsService;

    public InpostShipmentsController(ILogger<InpostShipmentsController> logger,
        IInpostShipmentRecordsService inpostShipmentRecordsService) : base(logger)
    {
        _inpostShipmentRecordsService = inpostShipmentRecordsService;
    }

    [HttpGet]
    [Produces<PaginatedResult<InpostShipmentRecordVm>>]
    public IActionResult GetPaginated(string? search, int? page, int? pageSize, string? sortColumn,
        SortOrder? sortOrder)
    {
        return ReturnOkResult(() =>
            _inpostShipmentRecordsService.GetPaginated(search, page, pageSize, sortColumn, sortOrder));
    }

    [HttpDelete]
    public IActionResult Delete(int id)
    {
        return ReturnOkResult(() =>
        {
            _inpostShipmentRecordsService.Delete(id);
            return new { message = "Wpis przesyłki został usunięty." };
        });
    }
}
