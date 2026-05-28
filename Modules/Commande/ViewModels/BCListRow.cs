using System.Globalization;
using GestionCommerciale.Modules.Commande.Models;
using GestionCommerciale.Shared.Helpers;
using GestionCommerciale.Shared.Services;

namespace GestionCommerciale.Modules.Commande.ViewModels;

public sealed class BCListRow
{
    public required BonCommande Bc { get; init; }
    public string FournisseurNom { get; init; } = string.Empty;
    public string DateShort { get; init; } = string.Empty;
    public string HtLabel { get; init; } = string.Empty;
    public string TtcLabel { get; init; } = string.Empty;
    public string NotePreview { get; init; } = string.Empty;

    public static BCListRow Create(BonCommande bc, string fournisseurNom, string devise, ILocaleService locale)
    {
        var (ht, _, ttc) = DocumentTotalsHelper.BonCommandeTotals(bc.Lignes ?? []);
        return new BCListRow
        {
            Bc = bc,
            FournisseurNom = fournisseurNom,
            DateShort = bc.Date.ToString("d", CultureInfo.CurrentCulture),
            HtLabel = locale.Tf("Doc_FmtHt", ht, devise),
            TtcLabel = $"{ttc:N2} {devise}",
            NotePreview = DocumentListFormat.NotePreview(bc.Note),
        };
    }
}
