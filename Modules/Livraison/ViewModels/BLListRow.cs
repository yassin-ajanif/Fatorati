using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using GestionCommerciale.Modules.Livraison.Models;
using GestionCommerciale.Shared.Helpers;
using GestionCommerciale.Shared.Services;

namespace GestionCommerciale.Modules.Livraison.ViewModels;

public partial class BLListRow : ObservableObject
{
    public required BonLivraison Bl { get; init; }
    public string ClientNom { get; init; } = string.Empty;
    public string DateShort { get; init; } = string.Empty;
    public string HtLabel { get; init; } = string.Empty;
    public string TtcLabel { get; init; } = string.Empty;
    public string NotePreview { get; init; } = string.Empty;

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _invoicedLabel = string.Empty;

    public bool CanInvoice => Bl.FactureId == null;

    public static BLListRow Create(BonLivraison bl, string clientNom, string devise, ILocaleService locale)
    {
        var (ht, _, ttc) = DocumentTotalsHelper.BonLivraisonTotals(bl.Lignes ?? []);
        return new BLListRow
        {
            Bl = bl,
            ClientNom = clientNom,
            DateShort = bl.Date.ToString("d", CultureInfo.CurrentCulture),
            HtLabel = locale.Tf("Doc_FmtHt", ht, devise),
            TtcLabel = $"{ttc:N2} {devise}",
            NotePreview = DocumentListFormat.NotePreview(bl.Note),
        };
    }
}
