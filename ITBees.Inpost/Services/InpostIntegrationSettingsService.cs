using ITBees.Inpost.Entities;
using ITBees.Inpost.Models;
using ITBees.Interfaces.Repository;

namespace ITBees.Inpost.Services;

/// <summary>
/// Ustawienia integracji InPost trzymane w bazie aplikacji hosta (jeden wiersz).
/// Aplikacja hosta musi zarejestrować encję przez ITBees.Inpost.Setup.DbModelBuilder.Register
/// oraz udostępniać generyczne repozytoria ITBees (IReadOnlyRepository/IWriteOnlyRepository).
/// Autoryzację zapewnia kontroler ([Authorize(Roles = "PlatformOperator")]).
/// </summary>
public class InpostIntegrationSettingsService : IInpostIntegrationSettingsService
{
    private readonly IReadOnlyRepository<InpostIntegrationSettings> _settingsRoRepo;
    private readonly IWriteOnlyRepository<InpostIntegrationSettings> _settingsWoRepo;

    public InpostIntegrationSettingsService(
        IReadOnlyRepository<InpostIntegrationSettings> settingsRoRepo,
        IWriteOnlyRepository<InpostIntegrationSettings> settingsWoRepo)
    {
        _settingsRoRepo = settingsRoRepo;
        _settingsWoRepo = settingsWoRepo;
    }

    public InpostIntegrationSettingsVm Get()
    {
        var settings = _settingsRoRepo.GetData(x => true).FirstOrDefault();
        return settings == null ? new InpostIntegrationSettingsVm() : new InpostIntegrationSettingsVm(settings);
    }

    public InpostIntegrationSettingsVm Update(InpostIntegrationSettingsIm im, Guid? modifiedByGuid = null)
    {
        var existing = _settingsRoRepo.GetData(x => true).FirstOrDefault();

        if (existing == null)
        {
            var inserted = _settingsWoRepo.InsertData(new InpostIntegrationSettings()
            {
                ApiToken = im.ApiToken?.Trim() ?? "",
                OrganizationId = im.OrganizationId?.Trim() ?? "",
                UseSandbox = im.UseSandbox,
                Modified = DateTime.Now,
                ModifiedByGuid = modifiedByGuid
            });

            return new InpostIntegrationSettingsVm(inserted);
        }

        var updated = _settingsWoRepo.UpdateData(x => x.Id == existing.Id, x =>
        {
            x.ApiToken = im.ApiToken?.Trim() ?? "";
            x.OrganizationId = im.OrganizationId?.Trim() ?? "";
            x.UseSandbox = im.UseSandbox;
            x.Modified = DateTime.Now;
            x.ModifiedByGuid = modifiedByGuid;
        }).First();

        return new InpostIntegrationSettingsVm(updated);
    }

    public InpostSettings? GetShipXSettingsOrNull()
    {
        var settings = _settingsRoRepo.GetData(x => true).FirstOrDefault();
        if (settings == null || string.IsNullOrWhiteSpace(settings.ApiToken) ||
            string.IsNullOrWhiteSpace(settings.OrganizationId))
        {
            return null;
        }

        return new InpostSettings
        {
            BaseUrl = settings.UseSandbox ? InpostSettings.SandboxBaseUrl : InpostSettings.ProductionBaseUrl,
            ApiKey = settings.ApiToken,
            OrganizationId = settings.OrganizationId
        };
    }
}
