# ITBees.Inpost - biblioteka komunikacji z InPost ShipX

Biblioteka pozwala tworzyć przesyłki (listy przewozowe) w API InPost ShipX:

- przesyłki paczkomatowe (`inpost_locker_standard`) - gabaryty A/B/C (`small`/`medium`/`large`),
- przesyłki kurierskie InPost (`inpost_courier_standard`) - gabaryty `small`/`medium`/`large`/`xlarge`,
- pobieranie numeru listu przewozowego (tracking number) oraz etykiety PDF.

Dodatkowo udostępnia wyszukiwarkę paczkomatów (`IInpostShipXClient.SearchParcelLockersAsync`
oraz endpoint `GET /InpostParcelLockers?search=`), dzięki której punkt docelowy wybiera się
z listy (po mieście, kodzie pocztowym lub kodzie paczkomatu) zamiast wpisywać kod ręcznie.

Zawiera też gotowy moduł ustawień integracji dla paneli administracyjnych ITBees
(wzorzec jak w ITBees.ServerStatus): encję `InpostIntegrationSettings` (token,
organization id, sandbox - przechowywane w bazie aplikacji hosta), serwis
`IInpostIntegrationSettingsService` oraz kontroler `GET/PUT /InpostIntegrationSettings`
(`[Authorize(Roles = "PlatformOperator")]`), wykrywany automatycznie przez ASP.NET.

## Podłączenie w aplikacji hosta (pełny moduł z kontrolerem)

```csharp
// DependencyRegistration:
new ITBees.Inpost.Setup.InpostSetup().Register(builder.Services);

// DbContext.OnModelCreating:
ITBees.Inpost.Setup.DbModelBuilder.Register(modelBuilder);
// + wygeneruj migrację EF tworzącą tabelę InpostIntegrationSettings
```

Wymagania: generyczne repozytoria ITBees (`IReadOnlyRepository<>`/`IWriteOnlyRepository<>`)
zarejestrowane w DI oraz rola `PlatformOperator` w autoryzacji.

## Rejestracja samego klienta ShipX

```csharp
services.AddInpostShipX(); // rejestruje IInpostShipXClient (ustawienia podajesz przy każdym wywołaniu)
// lub - gdy ustawienia pochodzą z appsettings.json (sekcja "Inpost"):
services.AddInpostClient(configuration);
```

Ustawienia (`InpostSettings`) przekazywane są do każdego wywołania klienta, dzięki czemu mogą
pochodzić z bazy danych aplikacji (np. konfigurowane w panelu administracyjnym):

```csharp
var settings = new InpostSettings
{
    BaseUrl = InpostSettings.ProductionBaseUrl, // lub SandboxBaseUrl
    ApiKey = "token ShipX z Menedżera Paczek",
    OrganizationId = "123456"
};
```

## Utworzenie przesyłki

```csharp
var order = new CreateShipmentOrder
{
    ShipmentType = InpostShipmentType.ParcelLocker,   // lub Courier
    ParcelTemplate = InpostParcelTemplates.Small,     // small/medium/large (+xlarge dla kuriera)
    TargetPoint = "KRA012",                           // wymagane dla paczkomatu
    ReceiverCompanyName = "Firma Sp. z o.o.",
    ReceiverEmail = "biuro@firma.pl",
    ReceiverPhone = "500600700",
    // dla kuriera dodatkowo: ReceiverStreet, ReceiverBuildingNumber, ReceiverCity, ReceiverPostCode
    Reference = "GPS-069243200900"
};

var result = await client.CreateShipmentAsync(settings, order);
if (result.Success)
{
    // tracking number może pojawić się po chwili - można poczekać:
    var tracked = await client.WaitForTrackingNumberAsync(settings, result.ShipmentId!, TimeSpan.FromSeconds(20));
    Console.WriteLine(tracked.TrackingNumber);
}
```

Przesyłka tworzona jest w trybie uproszczonym ShipX (samo `service` + `parcels.template`),
w którym oferta jest kupowana automatycznie - nie trzeba osobno potwierdzać oferty.

## Test w sandboxie

Projekt `InpostTestConsoleApp` tworzy testową przesyłkę w środowisku sandbox:

```
InpostTestConsoleApp <apiToken> <organizationId> [kodPaczkomatu]
```

Konto sandbox ShipX można założyć na https://sandbox-manager.paczkomaty.pl.
