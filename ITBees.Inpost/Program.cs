using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ITBees.Inpost.Services;

namespace ITBees.Inpost;

public static class InpostServiceCollectionExtensions
{
    /// <summary>
    /// Rejestruje klienta InPost ShipX. Ustawienia (token, organizacja) przekazuje się
    /// przy każdym wywołaniu metod <see cref="IInpostShipXClient"/> - mogą pochodzić z bazy danych.
    /// </summary>
    public static IServiceCollection AddInpostShipX(this IServiceCollection services)
    {
        services.AddHttpClient(InpostShipXClient.HttpClientName);
        services.AddTransient<IInpostShipXClient, InpostShipXClient>();
        return services;
    }

    /// <summary>
    /// Rejestruje klienta ShipX wraz z ustawieniami wczytanymi z konfiguracji (sekcja "Inpost").
    /// </summary>
    public static IServiceCollection AddInpostClient(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("Inpost").Get<InpostSettings>() ?? new InpostSettings();
        return services.AddInpostClient(settings);
    }

    public static IServiceCollection AddInpostClient(this IServiceCollection services, InpostSettings settings)
    {
        services.AddSingleton(settings);
        return services.AddInpostShipX();
    }
}
