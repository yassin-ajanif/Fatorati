using System.Globalization;
using GestionCommerciale.Modules.CommandeClient.Models;
using GestionCommerciale.Shared.Helpers;
using GestionCommerciale.Shared.Services;

namespace GestionCommerciale.Modules.CommandeClient.ViewModels;

public sealed class BCVListRow
{
    public required BonCommandeClient Bcc { get; init; }
    public string ClientNom { get; init; } = string.Empty;
    public string DateShort { get; init; } = string.Empty;
    public string HtLabel { get; init; } = string.Empty;
    public string TtcLabel { get; init; } = string.Empty;
    public string NotePreview { get; init; } = string.Empty;

    public static BCVListRow Create(BonCommandeClient bcc, string clientNom, string devise, ILocaleService locale)
    {
        var (ht, _, ttc) = BonCommandeClientTotals(bcc.Lignes ?? []);
        return new BCVListRow
        {
            Bcc = bcc,
            ClientNom = clientNom,
            DateShort = bcc.Date.ToString("d", CultureInfo.CurrentCulture),
            HtLabel = locale.Tf("Doc_FmtHt", ht, devise),
            TtcLabel = $"{ttc:N2} {devise}",
            NotePreview = DocumentListFormat.NotePreview(bcc.Note),
        };
    }

    private static (decimal ht, decimal tva, decimal ttc) BonCommandeClientTotals(IEnumerable<BonCommandeClientLigne> lignes)
    {
        decimal ht = 0, tva = 0;
        foreach (var l in lignes)
        {
            var lht = l.QuantiteCommandee * l.PrixUnitaireHT;
            ht += lht;
            tva += lht * (l.TauxTVA / 100m);
        }

        return (ht, tva, ht + tva);
    }
}
