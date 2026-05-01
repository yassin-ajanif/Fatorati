using GestionCommerciale.Modules.Devis.Models;
using GestionCommerciale.Modules.Facturation.Models;

namespace GestionCommerciale.Shared.Helpers;

public static class DocumentTotalsHelper
{
    public static decimal LigneHT(decimal qte, decimal puHt, decimal remisePct) =>
        qte * puHt * (1 - remisePct / 100m);

    public static (decimal ht, decimal tva, decimal ttc) DevisTotals(IEnumerable<DevisLigne> lignes, decimal remiseGlobalePct)
    {
        decimal ht = 0, tva = 0;
        foreach (var l in lignes)
        {
            var lht = LigneHT(l.Quantite, l.PrixUnitaireHT, l.Remise);
            ht += lht;
            tva += lht * (l.TauxTVA / 100m);
        }

        if (remiseGlobalePct > 0)
        {
            var factor = 1 - remiseGlobalePct / 100m;
            ht *= factor;
            tva *= factor;
        }

        return (ht, tva, ht + tva);
    }

    public static (decimal ht, decimal tva, decimal ttc) FactureTotals(IEnumerable<FactureLigne> lignes, decimal remiseGlobalePct)
    {
        decimal ht = 0, tva = 0;
        foreach (var l in lignes)
        {
            var lht = LigneHT(l.Quantite, l.PrixUnitaireHT, l.Remise);
            ht += lht;
            tva += lht * (l.TauxTVA / 100m);
        }

        if (remiseGlobalePct > 0)
        {
            var factor = 1 - remiseGlobalePct / 100m;
            ht *= factor;
            tva *= factor;
        }

        return (ht, tva, ht + tva);
    }

    public static (decimal ht, decimal tva, decimal ttc) AvoirTotals(IEnumerable<AvoirLigne> lignes)
    {
        decimal ht = 0, tva = 0;
        foreach (var l in lignes)
        {
            var lht = l.Quantite * l.PrixUnitaireHT;
            ht += lht;
            tva += lht * (l.TauxTVA / 100m);
        }

        return (ht, tva, ht + tva);
    }
}
