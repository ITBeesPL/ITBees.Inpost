# ITBees.Inpost - biblioteka komunikacji z InPost ShipX

Biblioteka pozwala tworzyć przesyłki (listy przewozowe) w API InPost ShipX:

- przesyłki paczkomatowe (`inpost_locker_standard`) - gabaryty A/B/C (`small`/`medium`/`large`),
- przesyłki kurierskie InPost (`inpost_courier_standard`) - gabaryty `small`/`medium`/`large`/`xlarge`,
- pobieranie numeru listu przewozowego (tracking number) oraz etykiety PDF,
- sprawdzanie w tle, czy przesyłka została już doręczona (etykiety doręczonej przesyłki nie
  drukujemy ponownie - patrz „Doręczenia”).

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

## Etykieta

`GET /InpostShipmentLabel?shipmentId=` zwraca etykietę PDF (dostępną dopiero po opłaceniu
przesyłki - inaczej 404 z powodem z ShipX). Parametr `type` wybiera typ etykiety ShipX
(`InpostLabelTypes`): bez niego (albo `normal`) - strona A4 do zwykłej drukarki, `type=A6` -
pojedyncza etykieta 105 × 148 mm dla drukarki etykiet (np. drukowanie natychmiastowe przez
ITBees.Printers). Nieznana wartość = etykieta domyślna. W kodzie:
`client.GetLabelWithDetailsAsync(settings, shipmentId, InpostLabelTypes.A6)`.

## Doręczenia

Etykiety doręczonej przesyłki nie drukujemy ponownie. Rekord listy przesyłek
(`InpostShipmentRecordVm`) ma flagę `IsDelivered` (status ShipX `delivered`) - panel ukrywa dla
takiej przesyłki wydruk etykiety, a `GET /InpostShipmentLabel` odpowiada **409** z powodem
w `message`. Przed wydrukiem etykiety przesyłki starszej niż doba jej status jest dodatkowo
sprawdzany w ShipX na bieżąco (sprawdzanie w tle może być do godziny do tyłu).

`InpostSetup.Register` uruchamia sprawdzanie w tle (`InpostDeliveryTrackingBackgroundService`):
pierwszy przebieg 5 minut po starcie aplikacji, potem co godzinę. Sprawdzane są przesyłki
utworzone w ShipX, starsze niż 24 h i nie starsze niż 60 dni, które nie mają jeszcze statusu
końcowego (doręczona, anulowana, zwrócona do nadawcy - `InpostShipmentStatuses.IsFinal`). Stan
jest wyłącznie odczytywany (`GET /v1/shipments/{id}`) - nic nie jest kupowane; zapisywany jest
status, a przy okazji numer listu, jeśli ShipX dokończył opłatę po czasie. Po 3 błędach ShipX
z rzędu przebieg się kończy, reszta przesyłek czeka na następny.

```csharp
new InpostSetup().Register(services, new InpostDeliveryTrackingSettings
{
    CheckInterval = TimeSpan.FromHours(1),   // co ile przebieg w tle
    MinShipmentAge = TimeSpan.FromHours(24), // młodszych przesyłek nie sprawdzamy
    MaxShipmentAge = TimeSpan.FromDays(60),  // starszych już nie sprawdzamy w tle; null = bez limitu
    Enabled = true                           // false = bez tła (blokada wydruku działa nadal)
});
```

Gdy z tej samej bazy korzysta kilka procesów, sprawdzanie w tle zostaw włączone tylko w jednym.
Nowych kolumn w bazie nie ma - status trzyma dotychczasowe pole `InpostShipment.Status`.

## Test w sandboxie

Projekt `InpostTestConsoleApp` tworzy testową przesyłkę w środowisku sandbox:

```
InpostTestConsoleApp <apiToken> <organizationId> [kodPaczkomatu]
```

Konto sandbox ShipX można założyć na https://sandbox-manager.paczkomaty.pl.
