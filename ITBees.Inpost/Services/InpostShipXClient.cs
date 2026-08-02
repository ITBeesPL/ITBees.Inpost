using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ITBees.Inpost.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ITBees.Inpost.Services;

/// <summary>
/// Klient API InPost ShipX (https://dokumentacja-inpost.atlassian.net/wiki/spaces/PL/overview).
/// Ustawienia (token, organizacja, środowisko) przekazywane są przy każdym wywołaniu,
/// dzięki czemu mogą pochodzić np. z bazy danych aplikacji.
/// </summary>
public class InpostShipXClient : IInpostShipXClient
{
    public const string HttpClientName = "InpostShipX";

    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // Jedyny publiczny konstruktor - drugi (z HttpClient) powodował błąd
    // "ambiguous constructors" przy walidacji DI, gdy aplikacja hosta rejestruje też HttpClient.
    public InpostShipXClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>Tworzy klienta bez kontenera DI (testy, aplikacje konsolowe).</summary>
    public static InpostShipXClient Create(HttpClient httpClient)
    {
        return new InpostShipXClient(new FixedHttpClientFactory(httpClient));
    }

    public async Task<InpostShipmentResult> CreateShipmentAsync(InpostSettings settings, CreateShipmentOrder order,
        CancellationToken ct = default)
    {
        var validationError = Validate(settings, order);
        if (validationError != null)
        {
            return new InpostShipmentResult { Success = false, ErrorMessage = validationError };
        }

        var body = BuildCreateShipmentBody(order);
        var url = $"{settings.BaseUrl.TrimEnd('/')}/v1/organizations/{settings.OrganizationId}/shipments";

        using var request = CreateRequest(HttpMethod.Post, url, settings);
        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8,
            "application/json");

        return await SendAndParseShipmentAsync(request, ct);
    }

    public async Task<InpostShipmentResult> GetShipmentAsync(InpostSettings settings, string shipmentId,
        CancellationToken ct = default)
    {
        var url = $"{settings.BaseUrl.TrimEnd('/')}/v1/shipments/{shipmentId}";
        using var request = CreateRequest(HttpMethod.Get, url, settings);
        return await SendAndParseShipmentAsync(request, ct);
    }

    public async Task<InpostShipmentResult> BuyShipmentOfferAsync(InpostSettings settings, string shipmentId,
        long? offerId = null, CancellationToken ct = default)
    {
        // ShipX wymaga wskazania oferty (offer_id) - bez tego zwraca błąd walidacji.
        if (offerId == null)
        {
            var shipment = await GetShipmentAsync(settings, shipmentId, ct);
            offerId = shipment.SelectedOfferId;

            if (offerId == null)
            {
                return new InpostShipmentResult
                {
                    Success = false,
                    ErrorMessage = "InPost nie przygotował jeszcze oferty przewozu dla tej przesyłki.",
                    Status = shipment.Status
                };
            }
        }

        var url = $"{settings.BaseUrl.TrimEnd('/')}/v1/shipments/{shipmentId}/buy";
        using var request = CreateRequest(HttpMethod.Post, url, settings);
        request.Content = new StringContent($"{{\"offer_id\":{offerId}}}", Encoding.UTF8, "application/json");

        return await SendAndParseShipmentAsync(request, ct);
    }

    public async Task<InpostShipmentResult> RefreshOffersAsync(InpostSettings settings, string shipmentId,
        CancellationToken ct = default)
    {
        // Udokumentowany sposób wygenerowania nowych ofert: pusty PUT na przesyłkę.
        var url = $"{settings.BaseUrl.TrimEnd('/')}/v1/shipments/{shipmentId}";
        using var request = CreateRequest(HttpMethod.Put, url, settings);
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        return await SendAndParseShipmentAsync(request, ct);
    }

    public async Task<InpostShipmentResult> WaitForTrackingNumberAsync(InpostSettings settings, string shipmentId,
        TimeSpan timeout, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        // W trybie uproszczonym (pole "service") ShipX sam wybiera i opłaca ofertę -
        // przez pierwsze sekundy nie przeszkadzamy mu własnym zakupem.
        var buyNotBefore = DateTime.UtcNow + AutoPurchaseGracePeriod;
        var buyAttempts = 0;
        var offersRefreshed = 0;
        InpostShipmentResult? failedBuy = null;
        InpostShipmentResult lastResult;

        do
        {
            lastResult = await GetShipmentAsync(settings, shipmentId, ct);
            if (!string.IsNullOrEmpty(lastResult.TrackingNumber))
            {
                return lastResult;
            }

            var canBuyNow = DateTime.UtcNow >= buyNotBefore
                            && buyAttempts < MaxBuyAttempts
                            && CanStillBeBought(lastResult.Status)
                            && lastResult.SelectedOfferId.HasValue;

            if (canBuyNow)
            {
                buyAttempts++;
                var buyResult = await BuyShipmentOfferAsync(settings, shipmentId, lastResult.SelectedOfferId, ct);

                if (buyResult.Success)
                {
                    failedBuy = null;
                }
                else
                {
                    failedBuy = buyResult;

                    // O przyczynie rozstrzyga transakcja rozliczeniowa ShipX, a nie treść błędu z /buy:
                    // przy zaległościach (debt_collection) /buy zwraca mylące offer_is_not_available.
                    var afterBuy = await GetShipmentAsync(settings, shipmentId, ct);
                    if (afterBuy.LastTransactionStatus == "failure")
                    {
                        lastResult = afterBuy;
                        failedBuy.ErrorKind = InpostErrorKind.AccountProblem;
                        break;
                    }

                    // Problem konta lub usługi InPost - ponawianie niczego nie zmieni.
                    if (buyResult.ErrorKind == InpostErrorKind.AccountProblem)
                    {
                        break;
                    }

                    // Oferty ShipX wygasają po kilku minutach; pusty PUT generuje nowe.
                    if (offersRefreshed < MaxOfferRefreshes && buyResult.ErrorKind == InpostErrorKind.OfferExpired)
                    {
                        offersRefreshed++;
                        buyAttempts = 0;
                        await RefreshOffersAsync(settings, shipmentId, ct);
                    }
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        } while (DateTime.UtcNow < deadline);

        if (string.IsNullOrEmpty(lastResult.TrackingNumber))
        {
            lastResult.ErrorKind = failedBuy?.ErrorKind ?? InpostErrorKind.Transient;
            lastResult.ErrorMessage = BuildNotPurchasedMessage(lastResult, failedBuy);
        }

        return lastResult;
    }

    private const int MaxBuyAttempts = 2;
    private const int MaxOfferRefreshes = 2;
    private static readonly TimeSpan AutoPurchaseGracePeriod = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Komunikat dla operatora budowany ze stanu przesyłki (transakcje, powody niedostępności ofert),
    /// a nie z surowej treści błędu - dzięki temu wskazuje właściwe miejsce naprawy.
    /// </summary>
    private static string BuildNotPurchasedMessage(InpostShipmentResult shipment, InpostShipmentResult? failedBuy)
    {
        var status = shipment.Status ?? "brak";

        if (!string.IsNullOrWhiteSpace(shipment.OfferUnavailabilityReasons))
        {
            return $"InPost nie może zrealizować tej przesyłki: {shipment.OfferUnavailabilityReasons}. " +
                   "Sprawdź, czy wybrany punkt obsługuje wybraną usługę.";
        }

        if (shipment.LastTransactionStatus == "failure" ||
            failedBuy?.ErrorKind == InpostErrorKind.AccountProblem)
        {
            var reason = shipment.LastTransactionError switch
            {
                "debt_collection" => "InPost odrzucił płatność (debt_collection - zaległości lub brak środków " +
                                     "na koncie InPost)",
                null or "" => "InPost nie opłacił przesyłki",
                _ => $"InPost odrzucił płatność ({shipment.LastTransactionError})"
            };

            return $"{reason}. Ureguluj saldo / dane rozliczeniowe w Menedżerze Paczek InPost " +
                   $"i użyj przycisku Odśwież. To nie jest błąd aplikacji. Status przesyłki: {status}.";
        }

        if (failedBuy?.ErrorKind == InpostErrorKind.DataOrCode && !string.IsNullOrWhiteSpace(failedBuy.ErrorMessage))
        {
            return failedBuy.ErrorMessage;
        }

        if (failedBuy?.ErrorKind == InpostErrorKind.OfferExpired)
        {
            var offerStatus = shipment.SelectedOfferStatus ?? "nieznany";
            return "InPost nie pozwolił opłacić oferty przewozu " +
                   $"(status przesyłki: {status}, status oferty: {offerStatus}). " +
                   "Jeśli powtórne Odśwież nie pomoże, sprawdź saldo i dane rozliczeniowe konta InPost " +
                   "w Menedżerze Paczek, albo utwórz list przewozowy ponownie.";
        }

        return $"InPost nie zakończył jeszcze opłacania przesyłki (status: {status}). " +
               "Użyj przycisku Odśwież za chwilę.";
    }

    /// <summary>
    /// Czy przesyłka w danym statusie może jeszcze zostać opłacona. Statusy końcowe
    /// (potwierdzona, w drodze, anulowana) nie wymagają już zakupu oferty.
    /// </summary>
    private static bool CanStillBeBought(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        return status is not ("confirmed" or "dispatched_by_sender" or "collected_from_sender"
            or "canceled" or "cancelled" or "delivered" or "returned");
    }

    public async Task<byte[]?> GetLabelAsync(InpostSettings settings, string shipmentId,
        CancellationToken ct = default)
    {
        return (await GetLabelWithDetailsAsync(settings, shipmentId, ct)).Content;
    }

    public async Task<InpostLabelResult> GetLabelWithDetailsAsync(InpostSettings settings, string shipmentId,
        CancellationToken ct = default)
    {
        var url = $"{settings.BaseUrl.TrimEnd('/')}/v1/shipments/{shipmentId}/label?format=Pdf";
        using var request = CreateRequest(HttpMethod.Get, url, settings);

        HttpResponseMessage response;
        try
        {
            response = await GetHttpClient().SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            return new InpostLabelResult { ErrorMessage = $"Błąd połączenia z API InPost: {ex.Message}" };
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);

            // Najczęstszy przypadek: etykieta powstaje dopiero po opłaceniu przesyłki.
            if (errorBody.Contains("shipment_status_incorrect"))
            {
                var shipment = await GetShipmentAsync(settings, shipmentId, ct);
                return new InpostLabelResult
                {
                    ErrorMessage =
                        "Etykieta powstaje dopiero po opłaceniu przesyłki przez InPost (status confirmed). " +
                        $"Obecny status przesyłki: {shipment.Status ?? "nieznany"}."
                };
            }

            return new InpostLabelResult
            {
                ErrorMessage = ExtractErrorMessage(errorBody, (int)response.StatusCode)
            };
        }

        return new InpostLabelResult { Content = await response.Content.ReadAsByteArrayAsync(ct) };
    }

    public async Task<List<InpostPointVm>> SearchParcelLockersAsync(InpostSettings settings, string? search,
        int limit = 25, CancellationToken ct = default)
    {
        var query = new List<string>
        {
            // parcel_locker zwraca także PaczkoPunkty (POP-*/POK-*), których usługa
            // inpost_locker_standard nie obsługuje - stąd parcel_locker_only.
            "type=parcel_locker_only",
            "status=Operating",
            $"per_page={Math.Clamp(limit, 1, 100)}"
        };

        var phrase = search?.Trim();
        if (!string.IsNullOrWhiteSpace(phrase))
        {
            // Wyszukiwarka punktów ShipX filtruje osobnymi parametrami - dobieramy je po kształcie frazy:
            // "31-000" to kod pocztowy, "KRA012" kod paczkomatu, pozostałe traktujemy jako miasto.
            if (LooksLikePostCode(phrase))
                query.Add($"post_code={Uri.EscapeDataString(phrase)}");
            else if (LooksLikePointCode(phrase))
                query.Add($"name={Uri.EscapeDataString(phrase.ToUpperInvariant())}");
            else
                query.Add($"city={Uri.EscapeDataString(phrase)}");
        }

        var url = $"{settings.BaseUrl.TrimEnd('/')}/v1/points?{string.Join("&", query)}";
        using var request = CreateRequest(HttpMethod.Get, url, settings);

        try
        {
            var response = await GetHttpClient().SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return new List<InpostPointVm>();
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            return ParsePoints(body);
        }
        catch (Exception)
        {
            return new List<InpostPointVm>();
        }
    }

    /// <summary>
    /// PaczkoPunkty i punkty obsługi klienta (POP-*/POK-*) nie realizują usługi Paczkomat®.
    /// </summary>
    private static bool IsServicePoint(string? pointName)
    {
        if (string.IsNullOrWhiteSpace(pointName))
        {
            return false;
        }

        var name = pointName.Trim().ToUpperInvariant();
        return name.StartsWith("POP-") || name.StartsWith("POK-") ||
               name.StartsWith("POP_") || name.StartsWith("POK_");
    }

    private static bool LooksLikePostCode(string phrase) =>
        phrase.Length is 5 or 6 && phrase.Count(char.IsDigit) == 5;

    private static bool LooksLikePointCode(string phrase) =>
        phrase.Length <= 12 && !phrase.Contains(' ') && phrase.Any(char.IsDigit) && phrase.Any(char.IsLetter);

    private static List<InpostPointVm> ParsePoints(string responseBody)
    {
        var points = new List<InpostPointVm>();

        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return points;
        }

        foreach (var item in items.EnumerateArray())
        {
            var name = GetAsString(item, "name");
            if (string.IsNullOrWhiteSpace(name) || IsServicePoint(name))
            {
                continue;
            }

            var point = new InpostPointVm
            {
                Name = name,
                LocationDescription = GetAsString(item, "location_description")
            };

            // Adres punktu: address_details ma rozbite pola, address tylko dwie linie tekstu.
            if (item.TryGetProperty("address_details", out var details) && details.ValueKind == JsonValueKind.Object)
            {
                var street = GetAsString(details, "street");
                var buildingNumber = GetAsString(details, "building_number");
                point.Street = string.Join(" ", new[] { street, buildingNumber }
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
                point.City = GetAsString(details, "city");
                point.PostCode = GetAsString(details, "post_code");
            }
            else if (item.TryGetProperty("address", out var address) && address.ValueKind == JsonValueKind.Object)
            {
                point.Street = GetAsString(address, "line1");
                point.City = GetAsString(address, "line2");
            }

            points.Add(point);
        }

        return points;
    }

    private static string? Validate(InpostSettings settings, CreateShipmentOrder order)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            return "Brak skonfigurowanego klucza API InPost (ShipX).";
        if (string.IsNullOrWhiteSpace(settings.OrganizationId))
            return "Brak skonfigurowanego identyfikatora organizacji InPost (ShipX).";
        if (!InpostParcelTemplates.IsValidFor(order.ShipmentType, order.ParcelTemplate))
            return $"Nieprawidłowy gabaryt przesyłki: {order.ParcelTemplate}.";
        if (order.ShipmentType == InpostShipmentType.ParcelLocker && string.IsNullOrWhiteSpace(order.TargetPoint))
            return "Dla przesyłki paczkomatowej wymagany jest kod paczkomatu docelowego (target point).";
        if (order.ShipmentType == InpostShipmentType.ParcelLocker && IsServicePoint(order.TargetPoint))
            return $"Punkt {order.TargetPoint} to PaczkoPunkt (POP), a nie Paczkomat - " +
                   "usługa Paczkomat® go nie obsługuje. Wybierz punkt typu Paczkomat.";
        if (order.ShipmentType == InpostShipmentType.Courier &&
            (string.IsNullOrWhiteSpace(order.ReceiverStreet) || string.IsNullOrWhiteSpace(order.ReceiverCity) ||
             string.IsNullOrWhiteSpace(order.ReceiverPostCode)))
            return "Dla przesyłki kurierskiej wymagany jest pełny adres odbiorcy (ulica, miasto, kod pocztowy).";

        return null;
    }

    private static Dictionary<string, object?> BuildCreateShipmentBody(CreateShipmentOrder order)
    {
        var receiver = new Dictionary<string, object?>
        {
            ["company_name"] = EmptyToNull(order.ReceiverCompanyName),
            ["first_name"] = EmptyToNull(order.ReceiverFirstName),
            ["last_name"] = EmptyToNull(order.ReceiverLastName),
            ["email"] = order.ReceiverEmail,
            ["phone"] = NormalizePhone(order.ReceiverPhone)
        };

        if (order.ShipmentType == InpostShipmentType.Courier)
        {
            receiver["address"] = new Dictionary<string, object?>
            {
                ["street"] = order.ReceiverStreet,
                ["building_number"] = EmptyToNull(order.ReceiverBuildingNumber) ?? "1",
                ["city"] = order.ReceiverCity,
                ["post_code"] = order.ReceiverPostCode,
                ["country_code"] = order.ReceiverCountryCode
            };
        }

        var body = new Dictionary<string, object?>
        {
            ["receiver"] = receiver,
            ["parcels"] = new object[] { new Dictionary<string, object?> { ["template"] = order.ParcelTemplate } },
            ["service"] = order.ShipmentType == InpostShipmentType.ParcelLocker
                ? "inpost_locker_standard"
                : "inpost_courier_standard",
            ["reference"] = EmptyToNull(order.Reference),
            ["comments"] = EmptyToNull(order.Comments)
        };

        if (order.ShipmentType == InpostShipmentType.ParcelLocker)
        {
            body["custom_attributes"] = new Dictionary<string, object?>
            {
                ["target_point"] = order.TargetPoint,
                ["sending_method"] = order.SendingMethod
            };
        }

        return body;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url, InpostSettings settings)
    {
        var request = new HttpRequestMessage(method, url);

        // Publiczne endpointy (np. wyszukiwarka punktów) działają bez tokenu.
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        }

        return request;
    }

    private async Task<InpostShipmentResult> SendAndParseShipmentAsync(HttpRequestMessage request,
        CancellationToken ct)
    {
        HttpResponseMessage response;
        string responseBody;
        try
        {
            response = await GetHttpClient().SendAsync(request, ct);
            responseBody = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            return new InpostShipmentResult
            {
                Success = false,
                ErrorMessage = $"Błąd połączenia z API InPost: {ex.Message}"
            };
        }

        if (!response.IsSuccessStatusCode)
        {
            return new InpostShipmentResult
            {
                Success = false,
                ErrorMessage = ExtractErrorMessage(responseBody, (int)response.StatusCode),
                ErrorKind = ClassifyError(responseBody, (int)response.StatusCode),
                RawJson = responseBody
            };
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var buyableOffer = ExtractBuyableOffer(root);
            var lastTransaction = ExtractLastTransaction(root);

            return new InpostShipmentResult
            {
                Success = true,
                ShipmentId = GetAsString(root, "id"),
                TrackingNumber = GetAsString(root, "tracking_number"),
                Status = GetAsString(root, "status"),
                SelectedOfferId = buyableOffer.offerId,
                SelectedOfferStatus = buyableOffer.status,
                OfferUnavailabilityReasons = buyableOffer.unavailabilityReasons,
                LastTransactionStatus = lastTransaction.status,
                LastTransactionError = lastTransaction.error,
                RawJson = responseBody
            };
        }
        catch (JsonException)
        {
            return new InpostShipmentResult
            {
                Success = false,
                ErrorMessage = "Nie udało się przetworzyć odpowiedzi API InPost.",
                RawJson = responseBody
            };
        }
    }

    private static string ExtractErrorMessage(string responseBody, int statusCode)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var message = GetAsString(root, "message") ?? GetAsString(root, "error");
            if (root.TryGetProperty("details", out var details))
            {
                message = $"{message} {details.GetRawText()}".Trim();
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                return $"InPost API ({statusCode}): {message}";
            }
        }
        catch (JsonException)
        {
            // odpowiedź nie jest JSON-em - zwracamy komunikat ogólny poniżej
        }

        return $"InPost API zwróciło błąd HTTP {statusCode}.";
    }

    /// <summary>
    /// Zwraca ofertę, którą ShipX pozwoli kupić - wyłącznie w statusie available/selected.
    /// Wysłanie do /buy oferty w innym statusie kończy się błędem offer_is_not_available.
    /// </summary>
    private static (long? offerId, string? status, string? unavailabilityReasons) ExtractBuyableOffer(
        JsonElement root)
    {
        string? firstStatus = null;
        var reasons = new List<string>();

        if (root.TryGetProperty("selected_offer", out var selectedOffer) &&
            selectedOffer.ValueKind == JsonValueKind.Object)
        {
            var status = GetAsString(selectedOffer, "status");
            firstStatus = status;

            if (IsBuyableStatus(status) &&
                selectedOffer.TryGetProperty("id", out var selectedId) &&
                selectedId.TryGetInt64(out var selectedOfferId))
            {
                return (selectedOfferId, status, null);
            }

            CollectUnavailabilityReasons(selectedOffer, reasons);
        }

        if (root.TryGetProperty("offers", out var offers) && offers.ValueKind == JsonValueKind.Array)
        {
            foreach (var offer in offers.EnumerateArray())
            {
                var status = GetAsString(offer, "status");
                firstStatus ??= status;
                CollectUnavailabilityReasons(offer, reasons);

                if (IsBuyableStatus(status) &&
                    offer.TryGetProperty("id", out var idElement) &&
                    idElement.TryGetInt64(out var offerId))
                {
                    return (offerId, status, null);
                }
            }
        }

        return (null, firstStatus, reasons.Count == 0 ? null : string.Join("; ", reasons.Distinct()));
    }

    private static bool IsBuyableStatus(string? status) => status is "available" or "selected";

    private static void CollectUnavailabilityReasons(JsonElement offer, List<string> reasons)
    {
        if (!offer.TryGetProperty("unavailability_reasons", out var element))
        {
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                reasons.AddRange(element.EnumerateArray()
                    .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.GetRawText())
                    .Where(x => !string.IsNullOrWhiteSpace(x))!);
                break;
            case JsonValueKind.String:
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    reasons.Add(value);
                }

                break;
        }
    }

    /// <summary>
    /// Stan ostatniej transakcji rozliczeniowej ShipX. To ona rozstrzyga, czy przesyłka nie została
    /// opłacona z powodu konta (np. debt_collection = zaległości/brak środków).
    /// </summary>
    private static (string? status, string? error) ExtractLastTransaction(JsonElement root)
    {
        if (!root.TryGetProperty("transactions", out var transactions) ||
            transactions.ValueKind != JsonValueKind.Array)
        {
            return (null, null);
        }

        string? lastStatus = null;
        string? lastError = null;

        foreach (var transaction in transactions.EnumerateArray())
        {
            var status = GetAsString(transaction, "status");
            if (string.IsNullOrWhiteSpace(status))
            {
                continue;
            }

            lastStatus = status;
            lastError = transaction.TryGetProperty("details", out var details) &&
                        details.ValueKind == JsonValueKind.Object
                ? GetAsString(details, "error") ?? GetAsString(details, "message")
                : null;
        }

        return (lastStatus, lastError);
    }

    /// <summary>
    /// Rozpoznaje rodzaj błędu ShipX po kodach w odpowiedzi. ShipX używa różnych kodów
    /// dla wygasłej oferty (można ponowić) i dla oferty niedostępnej (problem konta/usługi).
    /// </summary>
    private static InpostErrorKind ClassifyError(string responseBody, int statusCode)
    {
        if (statusCode is 401 or 403)
        {
            return InpostErrorKind.DataOrCode;
        }

        var body = responseBody.ToLowerInvariant();

        // Rozliczenia - ponawianie nic nie da, trzeba naprawić konto w Menedżerze Paczek.
        if (body.Contains("transaction_failed") || body.Contains("debt_collection") ||
            body.Contains("insufficient") || body.Contains("payment"))
            return InpostErrorKind.AccountProblem;

        // offer_is_not_available znaczy tylko tyle, że oferta nie jest w statusie available/selected -
        // najczęściej wygasła (oferty żyją ok. 5 minut). Rozwiązaniem jest wygenerowanie nowych ofert.
        if (body.Contains("offer_expired") || body.Contains("offer_is_not_available") ||
            body.Contains("offer_unavailable"))
            return InpostErrorKind.OfferExpired;

        if (body.Contains("shipment_locked"))
            return InpostErrorKind.Transient;

        return statusCode >= 500 ? InpostErrorKind.Transient : InpostErrorKind.DataOrCode;
    }

    private static string? GetAsString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    /// <summary>ShipX wymaga 9-cyfrowego numeru telefonu (bez prefiksu kraju).</summary>
    private static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length > 9 ? digits[^9..] : digits;
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private HttpClient GetHttpClient()
    {
        return _httpClientFactory.CreateClient(HttpClientName);
    }

    private sealed class FixedHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _httpClient;

        public FixedHttpClientFactory(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public HttpClient CreateClient(string name) => _httpClient;
    }
}
