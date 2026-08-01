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

    public async Task<InpostShipmentResult> WaitForTrackingNumberAsync(InpostSettings settings, string shipmentId,
        TimeSpan timeout, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        InpostShipmentResult lastResult;

        do
        {
            lastResult = await GetShipmentAsync(settings, shipmentId, ct);
            if (!string.IsNullOrEmpty(lastResult.TrackingNumber))
            {
                return lastResult;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        } while (DateTime.UtcNow < deadline);

        return lastResult;
    }

    public async Task<byte[]?> GetLabelAsync(InpostSettings settings, string shipmentId,
        CancellationToken ct = default)
    {
        var url = $"{settings.BaseUrl.TrimEnd('/')}/v1/shipments/{shipmentId}/label?format=Pdf";
        using var request = CreateRequest(HttpMethod.Get, url, settings);

        var response = await GetHttpClient().SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsByteArrayAsync(ct);
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
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
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
