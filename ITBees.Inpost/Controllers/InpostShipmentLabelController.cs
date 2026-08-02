using ITBees.Inpost.Services;
using ITBees.RestfulApiControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Inpost.Controllers;

/// <summary>
/// Pobieranie etykiety (listu przewozowego) PDF dla przesyłki utworzonej w ShipX.
/// </summary>
[Authorize(Roles = "PlatformOperator")]
public class InpostShipmentLabelController : RestfulControllerBase<InpostShipmentLabelController>
{
    private readonly IInpostIntegrationSettingsService _inpostIntegrationSettingsService;
    private readonly IInpostShipXClient _inpostShipXClient;

    public InpostShipmentLabelController(ILogger<InpostShipmentLabelController> logger,
        IInpostIntegrationSettingsService inpostIntegrationSettingsService,
        IInpostShipXClient inpostShipXClient) : base(logger)
    {
        _inpostIntegrationSettingsService = inpostIntegrationSettingsService;
        _inpostShipXClient = inpostShipXClient;
    }

    [HttpGet]
    public async Task<IActionResult> Get(string shipmentId)
    {
        var settings = _inpostIntegrationSettingsService.GetShipXSettingsOrNull();
        if (settings == null)
        {
            return BadRequest(new { message = "Integracja z InPost nie jest skonfigurowana." });
        }

        var pdf = await _inpostShipXClient.GetLabelAsync(settings, shipmentId);
        if (pdf == null)
        {
            // ShipX udostępnia etykietę dopiero po zakupie oferty - chwilę po utworzeniu przesyłki.
            return NotFound(new { message = "Etykieta nie jest jeszcze dostępna - spróbuj ponownie za chwilę." });
        }

        return File(pdf, "application/pdf", $"inpost-label-{shipmentId}.pdf");
    }
}
