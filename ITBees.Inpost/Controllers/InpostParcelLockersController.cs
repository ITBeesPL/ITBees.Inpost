using ITBees.Inpost.Models;
using ITBees.Inpost.Services;
using ITBees.RestfulApiControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Inpost.Controllers;

/// <summary>
/// Wyszukiwarka paczkomatów - pozwala wybrać punkt docelowy z listy
/// zamiast wpisywania kodu paczkomatu ręcznie.
/// </summary>
[Authorize(Roles = "PlatformOperator")]
public class InpostParcelLockersController : RestfulControllerBase<InpostParcelLockersController>
{
    private readonly IInpostIntegrationSettingsService _inpostIntegrationSettingsService;
    private readonly IInpostShipXClient _inpostShipXClient;

    public InpostParcelLockersController(ILogger<InpostParcelLockersController> logger,
        IInpostIntegrationSettingsService inpostIntegrationSettingsService,
        IInpostShipXClient inpostShipXClient) : base(logger)
    {
        _inpostIntegrationSettingsService = inpostIntegrationSettingsService;
        _inpostShipXClient = inpostShipXClient;
    }

    [HttpGet]
    [Produces<List<InpostPointVm>>]
    public async Task<IActionResult> Get(string? search, int? limit)
    {
        // Wyszukiwarka punktów działa też bez skonfigurowanego tokenu (API punktów jest publiczne),
        // dlatego przy braku ustawień korzystamy z domyślnego adresu produkcyjnego.
        var settings = _inpostIntegrationSettingsService.GetShipXSettingsOrNull() ?? new InpostSettings();

        var points = await _inpostShipXClient.SearchParcelLockersAsync(settings, search, limit ?? 25);
        return Ok(points);
    }
}
