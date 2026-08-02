using ITBees.Inpost.Services;
using ITBees.RestfulApiControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Inpost.Controllers;

/// <summary>
/// Surowa odpowiedź ShipX dla przesyłki - lista ofert, ich statusy i terminy ważności.
/// Pozwala operatorowi (lub wsparciu InPost) zobaczyć, dlaczego przesyłka nie została opłacona.
/// </summary>
[Authorize(Roles = "PlatformOperator")]
public class InpostShipmentDiagnosticsController : RestfulControllerBase<InpostShipmentDiagnosticsController>
{
    private readonly IInpostShipmentRecordsService _inpostShipmentRecordsService;

    public InpostShipmentDiagnosticsController(ILogger<InpostShipmentDiagnosticsController> logger,
        IInpostShipmentRecordsService inpostShipmentRecordsService) : base(logger)
    {
        _inpostShipmentRecordsService = inpostShipmentRecordsService;
    }

    [HttpGet]
    public IActionResult Get(int id)
    {
        return ReturnOkResult(() => new { rawJson = _inpostShipmentRecordsService.GetRawShipmentJson(id) });
    }
}
