using ITBees.Inpost.Models;
using ITBees.Inpost.Services;
using ITBees.RestfulApiControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Inpost.Controllers;

/// <summary>
/// Odświeżenie stanu przesyłki (status, numer listu przewozowego) z API InPost ShipX.
/// </summary>
[Authorize(Roles = "PlatformOperator")]
public class InpostShipmentStatusController : RestfulControllerBase<InpostShipmentStatusController>
{
    private readonly IInpostShipmentRecordsService _inpostShipmentRecordsService;

    public InpostShipmentStatusController(ILogger<InpostShipmentStatusController> logger,
        IInpostShipmentRecordsService inpostShipmentRecordsService) : base(logger)
    {
        _inpostShipmentRecordsService = inpostShipmentRecordsService;
    }

    [HttpGet]
    [Produces<InpostShipmentRecordVm>]
    public IActionResult Get(int id)
    {
        return ReturnOkResult(() => _inpostShipmentRecordsService.RefreshStatus(id));
    }
}
