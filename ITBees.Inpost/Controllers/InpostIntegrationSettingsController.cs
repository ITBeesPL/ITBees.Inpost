using System.Security.Claims;
using ITBees.Inpost.Models;
using ITBees.Inpost.Services;
using ITBees.RestfulApiControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Inpost.Controllers;

/// <summary>
/// Endpoint ustawień integracji InPost dla paneli administracyjnych ITBees.
/// Wykrywany automatycznie przez ASP.NET po dodaniu referencji do biblioteki;
/// wymagana rejestracja: InpostSetup.Register(services) + DbModelBuilder.Register(modelBuilder).
/// </summary>
[Authorize(Roles = "PlatformOperator")]
public class InpostIntegrationSettingsController : RestfulControllerBase<InpostIntegrationSettingsController>
{
    private readonly IInpostIntegrationSettingsService _inpostIntegrationSettingsService;

    public InpostIntegrationSettingsController(ILogger<InpostIntegrationSettingsController> logger,
        IInpostIntegrationSettingsService inpostIntegrationSettingsService) : base(logger)
    {
        _inpostIntegrationSettingsService = inpostIntegrationSettingsService;
    }

    [HttpGet]
    [Produces<InpostIntegrationSettingsVm>]
    public IActionResult Get()
    {
        return ReturnOkResult(() => _inpostIntegrationSettingsService.Get());
    }

    [HttpPut]
    [Produces<InpostIntegrationSettingsVm>]
    public IActionResult Put([FromBody] InpostIntegrationSettingsIm inpostIntegrationSettingsIm)
    {
        return ReturnOkResult(() =>
            _inpostIntegrationSettingsService.Update(inpostIntegrationSettingsIm, GetCurrentUserGuidOrNull()));
    }

    private Guid? GetCurrentUserGuidOrNull()
    {
        var id = User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var guid) ? guid : null;
    }
}
