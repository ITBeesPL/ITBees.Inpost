using ITBees.Inpost.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ITBees.Inpost.Setup;

public class InpostSetup
{
    /// <summary>
    /// Rejestruje klienta ShipX oraz serwis ustawień integracji (przechowywanych w bazie
    /// aplikacji hosta - patrz <see cref="DbModelBuilder.Register"/>). Kontroler
    /// InpostIntegrationSettingsController jest wykrywany automatycznie przez ASP.NET.
    /// Uruchamia też w tle sprawdzanie, czy nadane przesyłki zostały już doręczone
    /// (<paramref name="deliveryTracking"/>; bez nich - ustawienia domyślne, wyłączenie przez
    /// <see cref="InpostDeliveryTrackingSettings.Enabled"/> = false). Gdy z tej samej bazy korzysta kilka
    /// procesów, sprawdzanie w tle zostaw włączone w jednym z nich - inaczej każdy odpytywałby ShipX
    /// o te same przesyłki.
    /// </summary>
    public void Register(IServiceCollection services, InpostDeliveryTrackingSettings? deliveryTracking = null)
    {
        var trackingSettings = deliveryTracking ?? new InpostDeliveryTrackingSettings();

        services.AddInpostShipX();
        services.AddSingleton(trackingSettings);
        services.AddTransient<IInpostIntegrationSettingsService, InpostIntegrationSettingsService>();
        services.AddTransient<IInpostShipmentRecordsService, InpostShipmentRecordsService>();
        // Także poza tłem - blokuje wydruk etykiety doręczonej przesyłki (InpostShipmentLabelController).
        services.AddTransient<IInpostDeliveryTrackingService, InpostDeliveryTrackingService>();

        if (trackingSettings.Enabled)
        {
            services.AddHostedService<InpostDeliveryTrackingBackgroundService>();
        }
    }
}
