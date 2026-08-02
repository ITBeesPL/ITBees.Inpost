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

    public async Task<InpostShipmentResult> WaitForTrackingNumberAsync(InpostSettings settings, string shipmentId,
        TimeSpan timeout, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        var buyAttempts = 0;
        string? lastBuyError = null;
        InpostShipmentResult lastResult;

        do
        {
            lastResult = await GetShipmentAsync(settings, shipmentId, ct);
            if (!string.IsNullOrEmpty(lastResult.TrackingNumber))
            {
                return lastResult;
            }

            // ShipX przygotowuje oferty asynchronicznie; dopóki oferta nie zostanie kupiona,
            // przesyłka nie dostanie numeru listu przewozowego ani etykiety. Statusy bywają
            // różne w zależności od usługi, więc próbujemy zakupu dla każdego stanu,
            // z którego przesyłka może jeszcze przejść dalej.
            if (buyAttempts < MaxBuyAttempts && CanStillBeBought(lastResult.Status) &&
                lastResult.SelectedOfferId.HasValue)
            {
                buyAttempts++;
                var buyResult = await BuyShipmentOfferAsync(settings, shipmentId, lastResult.SelectedOfferId, ct);
                if (buyResult.Success)
                {
                    lastBuyError = null;
                }
                else
                {
                    lastBuyError = buyResult.ErrorMessage;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        } while (DateTime.UtcNow < deadline);

        // Bez numeru listu warto pokazać operatorowi, dlaczego zakup oferty się nie powiódł.
        if (string.IsNullOrEmpty(lastResult.TrackingNumber))
        {
            lastResult.ErrorMessage = lastBuyError ?? (lastResult.SelectedOfferId == null
                ? $"InPost nie przygotował jeszcze oferty przewozu (status: {lastResult.Status ?? "brak"}). " +
                  "Użyj przycisku Odśwież za chwilę."
                : $"Przesyłka czeka na opłacenie oferty (status: {lastResult.Status ?? "brak"}). " +
                  "Użyj przycisku Odśwież, aby dokończyć zakup.");
        }

        return lastResult;
    }

    private const int MaxBuyAttempts = 3;

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
            "type=parcel_locker",
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
            if (string.IsNullOrWhiteSpace(name))
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
                RawJson = responseBody
            };
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            return new InpostShipmentResult
            {
                Success = true,
                ShipmentId = GetAsString(root, "id"),
                TrackingNumber = GetAsString(root, "tracking_number"),
                Status = GetAsString(root, "status"),
                SelectedOfferId = ExtractSelectedOfferId(root),
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
    /// Wyciąga id oferty do zakupu: najpierw ofertę wybraną przez ShipX (selected_offer),
    /// a gdy jej nie ma - pierwszą dostępną z listy offers.
    /// </summary>
    private static long? ExtractSelectedOfferId(JsonElement root)
    {
        if (root.TryGetProperty("selected_offer", out var selectedOffer) &&
            selectedOffer.ValueKind == JsonValueKind.Object &&
            selectedOffer.TryGetProperty("id", out var selectedId) &&
            selectedId.TryGetInt64(out var selectedOfferId))
        {
            return selectedOfferId;
        }

        if (!root.TryGetProperty("offers", out var offers) || offers.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        long? firstOfferId = null;
        foreach (var offer in offers.EnumerateArray())
        {
            if (!offer.TryGetProperty("id", out var idElement) || !idElement.TryGetInt64(out var offerId))
            {
                continue;
            }

            firstOfferId ??= offerId;

            if (GetAsString(offer, "status") == "available")
            {
                return offerId;
            }
        }

        return firstOfferId;
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
