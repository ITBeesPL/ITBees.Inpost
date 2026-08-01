using ITBees.Inpost;
using ITBees.Inpost.Models;
using ITBees.Inpost.Services;

namespace InpostTestConsoleApp
{
    internal class Program
    {
        /// <summary>
        /// Testowe utworzenie przesyłki w sandboxie ShipX.
        /// Użycie: InpostTestConsoleApp &lt;apiToken&gt; &lt;organizationId&gt; [targetPoint]
        /// </summary>
        static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Użycie: InpostTestConsoleApp <apiToken> <organizationId> [targetPoint]");
                return;
            }

            var settings = new InpostSettings
            {
                BaseUrl = InpostSettings.SandboxBaseUrl,
                ApiKey = args[0],
                OrganizationId = args[1]
            };

            var client = InpostShipXClient.Create(new HttpClient());

            var order = new CreateShipmentOrder
            {
                ShipmentType = InpostShipmentType.ParcelLocker,
                ParcelTemplate = InpostParcelTemplates.Small,
                TargetPoint = args.Length > 2 ? args[2] : "KRA012",
                ReceiverCompanyName = "Firma testowa",
                ReceiverEmail = "test@example.com",
                ReceiverPhone = "500600700",
                Reference = "TEST-001"
            };

            var result = await client.CreateShipmentAsync(settings, order);
            Console.WriteLine($"Success: {result.Success}");
            Console.WriteLine($"ShipmentId: {result.ShipmentId}");
            Console.WriteLine($"Status: {result.Status}");
            Console.WriteLine($"Error: {result.ErrorMessage}");

            if (result.Success && result.ShipmentId != null)
            {
                var withTracking = await client.WaitForTrackingNumberAsync(settings, result.ShipmentId,
                    TimeSpan.FromSeconds(20));
                Console.WriteLine($"TrackingNumber: {withTracking.TrackingNumber ?? "(jeszcze niedostępny)"}");
            }
        }
    }
}
