using ITBees.Inpost.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ITBees.Inpost.Setup;

public class InpostSetup
{
    /// <summary>
    /// Rejestruje klienta ShipX oraz serwis ustawień integracji (przechowywanych w bazie
    /// aplikacji hosta - patrz <see cref="DbModelBuilder.Register"/>). Kontroler
    /// InpostIntegrationSettingsController jest wykrywany automatycznie przez ASP.NET.
    /// </summary>
    public void Register(IServiceCollection services)
    {
        services.AddInpostShipX();
        services.AddTransient<IInpostIntegrationSettingsService, InpostIntegrationSettingsService>();
    }
}
