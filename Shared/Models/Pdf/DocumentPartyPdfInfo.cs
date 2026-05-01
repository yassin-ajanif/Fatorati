using GestionCommerciale.Modules.Tiers.Models;

namespace GestionCommerciale.Shared.Models.Pdf;

/// <summary>Client or supplier identity for PDF party panel.</summary>
public sealed record DocumentPartyPdfInfo(string Nom, string? Ice = null, string? Adresse = null)
{
    public static DocumentPartyPdfInfo FromTiers(Tiers t)
    {
        var addr = string.Join(", ", new[] { t.Adresse, t.Ville }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return new DocumentPartyPdfInfo(
            t.Nom,
            string.IsNullOrWhiteSpace(t.ICE) ? null : t.ICE.Trim(),
            string.IsNullOrWhiteSpace(addr) ? null : addr);
    }
}
