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

        var label = await _inpostShipXClient.GetLabelWithDetailsAsync(settings, shipmentId);
        if (label.Content == null)
        {
            // ShipX udostępnia etykietę dopiero po zakupie oferty - pokazujemy konkretny powód z API.
            return NotFound(new
            {
                message = label.ErrorMessage ??
                          "Etykieta nie jest jeszcze dostępna - użyj przycisku Odśwież, aby dokończyć zakup oferty."
            });
        }

        return File(label.Content, "application/pdf", $"inpost-label-{shipmentId}.pdf");
    }
}
