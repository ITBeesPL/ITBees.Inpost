namespace ITBees.Inpost.Models;

/// <summary>
/// Szablony (gabaryty) przesyłek oferowane przez InPost ShipX.
/// </summary>
public static class InpostParcelTemplates
{
    /// <summary>Gabaryt A - 8 x 38 x 64 cm, do 25 kg.</summary>
    public const string Small = "small";

    /// <summary>Gabaryt B - 19 x 38 x 64 cm, do 25 kg.</summary>
    public const string Medium = "medium";

    /// <summary>Gabaryt C - 41 x 38 x 64 cm, do 25 kg.</summary>
    public const string Large = "large";

    /// <summary>Gabaryt D - 50 x 50 x 80 cm, do 25 kg (tylko kurier).</summary>
    public const string XLarge = "xlarge";

    public static readonly string[] ParcelLockerTemplates = { Small, Medium, Large };
    public static readonly string[] CourierTemplates = { Small, Medium, Large, XLarge };

    public static bool IsValidFor(InpostShipmentType shipmentType, string template)
    {
        return shipmentType == InpostShipmentType.ParcelLocker
            ? ParcelLockerTemplates.Contains(template)
            : CourierTemplates.Contains(template);
    }
}
