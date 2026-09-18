using ITBees.Inpost.Models;
using ITBees.Inpost.Services;
using ITBees.RestfulApiControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Inpost.Controllers;

/// <summary>
/// Pobieranie etykiety (listu przewozowego) PDF dla przesyłki utworzonej w ShipX. Parametr
/// <c>type</c> wybiera typ etykiety (<see cref="InpostLabelTypes"/>): bez niego strona A4,
/// <c>type=A6</c> - pojedyncza etykieta 105 × 148 mm dla drukarki etykiet.
/// Etykiety doręczonej przesyłki nie wydaje (409 z powodem w <c>message</c>).
/// </summary>
[Authorize(Roles = "PlatformOperator")]
public class InpostShipmentLabelController : RestfulControllerBase<InpostShipmentLabelController>
{
    private readonly IInpostIntegrationSettingsService _inpostIntegrationSettingsService;
    private readonly IInpostShipXClient _inpostShipXClient;
    private readonly IInpostDeliveryTrackingService _inpostDeliveryTrackingService;

    public InpostShipmentLabelController(ILogger<InpostShipmentLabelController> logger,
        IInpostIntegrationSettingsService inpostIntegrationSettingsService,
        IInpostShipXClient inpostShipXClient,
        IInpostDeliveryTrackingService inpostDeliveryTrackingService) : base(logger)
    {
        _inpostIntegrationSettingsService = inpostIntegrationSettingsService;
        _inpostShipXClient = inpostShipXClient;
        _inpostDeliveryTrackingService = inpostDeliveryTrackingService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(string shipmentId, string? type = null)
    {
        var settings = _inpostIntegrationSettingsService.GetShipXSettingsOrNull();
        if (settings == null)
        {
            return BadRequest(new { message = "Integracja z InPost nie jest skonfigurowana." });
        }

        var blockReason = await _inpostDeliveryTrackingService.GetLabelBlockReasonAsync(shipmentId);
        if (blockReason != null)
        {
            return Conflict(new { message = blockReason });
        }

        var labelType = InpostLabelTypes.Normalize(type);
        var label = await _inpostShipXClient.GetLabelWithDetailsAsync(settings, shipmentId, labelType);
        if (label.Content == null)
        {
            // ShipX udostępnia etykietę dopiero po zakupie oferty - pokazujemy konkretny powód z API.
            return NotFound(new
            {
                message = label.ErrorMessage ??
                          "Etykieta nie jest jeszcze dostępna - użyj przycisku Odśwież, aby dokończyć zakup oferty."
            });
        }

        var suffix = labelType == InpostLabelTypes.A6 ? "-a6" : string.Empty;
        return File(label.Content, "application/pdf", $"inpost-label-{shipmentId}{suffix}.pdf");
    }
}
