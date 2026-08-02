namespace ITBees.Inpost.Models;

/// <summary>
/// Rodzaj problemu zgłoszonego przez ShipX - decyduje o tym, czy ponawiać operację
/// i czy komunikat ma kierować operatora do aplikacji, czy do Menedżera Paczek InPost.
/// </summary>
public enum InpostErrorKind
{
    None = 0,

    /// <summary>Stan przejściowy (np. trwa kalkulacja) - warto ponowić.</summary>
    Transient,

    /// <summary>Oferta wygasła - trzeba poprosić o nowe oferty i kupić ponownie.</summary>
    OfferExpired,

    /// <summary>Problem konta InPost (saldo, dane rozliczeniowe, brak usługi) - ponawianie nic nie da.</summary>
    AccountProblem,

    /// <summary>Błędne dane przesyłki lub konfiguracja integracji.</summary>
    DataOrCode
}
