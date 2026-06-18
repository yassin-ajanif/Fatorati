using GestionCommerciale.Modules.AvoirFournisseur.Models;
using GestionCommerciale.Modules.Commande.Models;
using GestionCommerciale.Modules.Devis.Models;
using GestionCommerciale.Modules.Facturation.Models;
using GestionCommerciale.Modules.Livraison.Models;
using GestionCommerciale.Modules.Reception.Models;
using GestionCommerciale.Shared.Database;
using GestionCommerciale.Shared.Helpers;
using GestionCommerciale.Shared.Models.Pdf;
using GestionCommerciale.Shared.Services.Pdf;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace GestionCommerciale.Shared.Services;

public sealed class PdfService : IPdfService
{
    private readonly IAppSettingsService _settings;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IUiPreferencesService _uiPreferences;

    public PdfService(
        IAppSettingsService settings,
        IDbContextFactory<AppDbContext> dbFactory,
        IUiPreferencesService uiPreferences)
    {
        _settings = settings;
        _dbFactory = dbFactory;
        _uiPreferences = uiPreferences;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static readonly CultureInfo PdfCulture = CultureInfo.GetCultureInfo("fr-FR");

    private static string FmtQty(decimal value) => value.ToString("#,##0.##", PdfCulture);

    private static string FmtUnitPrice(decimal value) => value.ToString("#,##0.##", PdfCulture);

    private static string FmtTvaPct(decimal value) => value.ToString("#,##0.##", PdfCulture);

    private static string FmtMoney(decimal value) => value.ToString("N2", PdfCulture);

    public async Task<byte[]> BuildDevisPdfAsync(Devis devis, DocumentPartyPdfInfo party, CancellationToken cancellationToken = default)
    {
        var cfg = await _settings.GetAsync(cancellationToken);
        var refs = await LoadProductRefsAsync(devis.Lignes.Select(l => l.ProduitId), cancellationToken);
        var totals = DocumentTotalsHelper.DevisTotals(devis.Lignes, devis.RemiseGlobale);
        var vis = _uiPreferences.GetDocumentLineColumnVisibility("devis");
        var lineData = new List<StandardPdfLine>();
        foreach (var l in devis.Lignes)
        {
            var ht = DocumentTotalsHelper.LigneHT(l.Quantite, l.PrixUnitaireHT, l.Remise);
            var ttc = ht * (1 + l.TauxTVA / 100m);
            lineData.Add(new StandardPdfLine(
                RefCell(refs, l.ProduitId),
                l.Designation,
                FmtQty(l.Quantite),
                l.Conditionnement,
                FmtUnitPrice(l.PrixUnitaireHT),
                FmtTvaPct(l.TauxTVA),
                FmtMoney(l.Remise),
                FmtMoney(ht),
                FmtMoney(ttc)));
        }

        var (cols, rows) = BuildStandardPdfTable(vis, supportsLineRemise: true, "Qté", lineData);

        var docLines = new List<PdfKeyValueLine>
        {
            new("N°", devis.Numero),
            new("Date", devis.Date.ToString("dd/MM/yyyy")),
            new("Valable jusqu'au", devis.DateValidite.ToString("dd/MM/yyyy"))
        };
        if (devis.RemiseGlobale > 0)
            docLines.Add(new("Remise globale", $"{devis.RemiseGlobale:N2} %"));

        var model = BaseModel(cfg, "DEVIS", docLines, PartyLines(party, "Client"), cols, rows, totals, devis.Note, vis.ShowMontantTtc);
        return CommercialDocumentPdfRenderer.Render(model, TryLoadLogoBytes(cfg.SocieteLogoPath));
    }

    public async Task<byte[]> BuildBonLivraisonPdfAsync(BonLivraison bl, DocumentPartyPdfInfo party, CancellationToken cancellationToken = default)
    {
        var cfg = await _settings.GetAsync(cancellationToken);
        var refs = await LoadProductRefsAsync(bl.Lignes.Select(l => l.ProduitId), cancellationToken);
        var blVis = _uiPreferences.GetDocumentLineColumnVisibility("bon_livraison");
        decimal ht = 0, tva = 0;
        var lineData = new List<BlPdfLine>();
        foreach (var l in bl.Lignes)
        {
            var lht = l.QuantiteLivree * l.PrixUnitaireHT;
            ht += lht;
            tva += lht * (l.TauxTVA / 100m);
            var ttc = lht * (1 + l.TauxTVA / 100m);
            lineData.Add(new BlPdfLine(
                RefCell(refs, l.ProduitId),
                l.Designation,
                FmtQty(l.QuantiteCommandee),
                FmtQty(l.QuantiteLivree),
                FmtUnitPrice(l.PrixUnitaireHT),
                FmtTvaPct(l.TauxTVA),
                FmtMoney(lht),
                FmtMoney(ttc)));
        }

        var (cols, rows) = BuildBlPdfTable(blVis, lineData);

        var docLines = new List<PdfKeyValueLine>
        {
            new("N°", bl.Numero),
            new("Date", bl.Date.ToString("dd/MM/yyyy"))
        };

        var model = BaseModel(cfg, "BON DE LIVRAISON", docLines, PartyLines(party, "Client"), cols, rows, (ht, tva, ht + tva), bl.Note, blVis.ShowMontantTtc);
        return CommercialDocumentPdfRenderer.Render(model, TryLoadLogoBytes(cfg.SocieteLogoPath));
    }

    public async Task<byte[]> BuildBonReceptionPdfAsync(BonReception br, DocumentPartyPdfInfo party, CancellationToken cancellationToken = default)
    {
        var cfg = await _settings.GetAsync(cancellationToken);
        var refs = await LoadProductRefsAsync(br.Lignes.Select(l => l.ProduitId), cancellationToken);
        decimal ht = 0, tva = 0;
        var cols = BrColumns();
        var rows = new List<IReadOnlyList<string>>();
        foreach (var l in br.Lignes)
        {
            var lht = l.QuantiteRecue * l.PrixUnitaireHT;
            ht += lht;
            tva += lht * (l.TauxTVA / 100m);
            var ttc = lht * (1 + l.TauxTVA / 100m);
            rows.Add([
                RefCell(refs, l.ProduitId),
                l.Designation,
                FmtQty(l.QuantiteRecue),
                FmtUnitPrice(l.PrixUnitaireHT),
                FmtTvaPct(l.TauxTVA),
                FmtMoney(lht),
                FmtMoney(ttc)
            ]);
        }

        var docLines = new List<PdfKeyValueLine>
        {
            new("N°", br.Numero),
            new("Date", br.Date.ToString("dd/MM/yyyy"))
        };

        var model = BaseModel(cfg, "BON DE RÉCEPTION", docLines, PartyLines(party, "Fournisseur"), cols, rows, (ht, tva, ht + tva), br.Note);
        return CommercialDocumentPdfRenderer.Render(model, TryLoadLogoBytes(cfg.SocieteLogoPath));
    }

    public async Task<byte[]> BuildBonCommandePdfAsync(BonCommande bc, DocumentPartyPdfInfo party, CancellationToken cancellationToken = default)
    {
        var cfg = await _settings.GetAsync(cancellationToken);
        var refs = await LoadProductRefsAsync(bc.Lignes.Select(l => l.ProduitId), cancellationToken);
        decimal ht = 0, tva = 0;
        var vis = _uiPreferences.GetDocumentLineColumnVisibility("bon_commande");
        var lineData = new List<StandardPdfLine>();
        foreach (var l in bc.Lignes)
        {
            var lht = l.QuantiteCommandee * l.PrixUnitaireHT;
            ht += lht;
            tva += lht * (l.TauxTVA / 100m);
            var ttc = lht * (1 + l.TauxTVA / 100m);
            lineData.Add(new StandardPdfLine(
                RefCell(refs, l.ProduitId),
                l.Designation,
                FmtQty(l.QuantiteCommandee),
                l.Conditionnement,
                FmtUnitPrice(l.PrixUnitaireHT),
                FmtTvaPct(l.TauxTVA),
                "—",
                FmtMoney(lht),
                FmtMoney(ttc)));
        }

        var (cols, rows) = BuildStandardPdfTable(vis, supportsLineRemise: false, "Qté cmd.", lineData);

        var docLines = new List<PdfKeyValueLine>
        {
            new("N°", bc.Numero),
            new("Date", bc.Date.ToString("dd/MM/yyyy"))
        };

        var model = BaseModel(cfg, "BON DE COMMANDE", docLines, PartyLines(party, "Fournisseur"), cols, rows, (ht, tva, ht + tva), bc.Note, vis.ShowMontantTtc);
        return CommercialDocumentPdfRenderer.Render(model, TryLoadLogoBytes(cfg.SocieteLogoPath));
    }

    public async Task<byte[]> BuildFacturePdfAsync(Facture facture, DocumentPartyPdfInfo party, CancellationToken cancellationToken = default)
    {
        var cfg = await _settings.GetAsync(cancellationToken);
        var refs = await LoadProductRefsAsync(facture.Lignes.Select(l => l.ProduitId), cancellationToken);
        var totals = DocumentTotalsHelper.FactureTotals(facture.Lignes, facture.RemiseGlobale);
        var vis = _uiPreferences.GetDocumentLineColumnVisibility("facture");
        var lineData = new List<StandardPdfLine>();
        foreach (var l in facture.Lignes)
        {
            var lht = DocumentTotalsHelper.LigneHT(l.Quantite, l.PrixUnitaireHT, l.Remise);
            var ttc = lht * (1 + l.TauxTVA / 100m);
            lineData.Add(new StandardPdfLine(
                RefCell(refs, l.ProduitId),
                l.Designation,
                FmtQty(l.Quantite),
                l.Conditionnement,
                FmtUnitPrice(l.PrixUnitaireHT),
                FmtTvaPct(l.TauxTVA),
                FmtMoney(l.Remise),
                FmtMoney(lht),
                FmtMoney(ttc)));
        }

        var (cols, rows) = BuildStandardPdfTable(vis, supportsLineRemise: true, "Qté", lineData);

        var docLines = new List<PdfKeyValueLine>
        {
            new("N°", facture.Numero),
            new("Date", facture.Date.ToString("dd/MM/yyyy")),
            new("Échéance", facture.DateEcheance.ToString("dd/MM/yyyy"))
        };
        var pay = SummarizePaiements(facture.Paiements);
        if (!string.IsNullOrWhiteSpace(pay))
            docLines.Add(new("Payé par", pay!));
        if (facture.RemiseGlobale > 0)
            docLines.Add(new("Remise globale", $"{facture.RemiseGlobale:N2} %"));

        var model = BaseModel(cfg, "FACTURE", docLines, PartyLines(party, "Client"), cols, rows, totals, facture.Note, vis.ShowMontantTtc);
        return CommercialDocumentPdfRenderer.Render(model, TryLoadLogoBytes(cfg.SocieteLogoPath));
    }

    public async Task<byte[]> BuildAvoirPdfAsync(Avoir avoir, DocumentPartyPdfInfo party, CancellationToken cancellationToken = default)
    {
        var cfg = await _settings.GetAsync(cancellationToken);
        var refs = await LoadProductRefsAsync(avoir.Lignes.Select(l => l.ProduitId), cancellationToken);
        var totals = DocumentTotalsHelper.AvoirTotals(avoir.Lignes);
        var cols = BrColumns();
        var rows = new List<IReadOnlyList<string>>();
        foreach (var l in avoir.Lignes)
        {
            var lht = l.Quantite * l.PrixUnitaireHT;
            var ttc = lht * (1 + l.TauxTVA / 100m);
            rows.Add([
                RefCell(refs, l.ProduitId),
                l.Designation,
                FmtQty(l.Quantite),
                FmtUnitPrice(l.PrixUnitaireHT),
                FmtTvaPct(l.TauxTVA),
                FmtMoney(lht),
                FmtMoney(ttc)
            ]);
        }

        var note = $"{avoir.Motif}\nRetour marchandise : {(avoir.RetourMarchandise ? "Oui" : "Non")}";
        var docLines = new List<PdfKeyValueLine>
        {
            new("N°", avoir.Numero),
            new("Date", avoir.Date.ToString("dd/MM/yyyy"))
        };

        var model = BaseModel(cfg, "AVOIR", docLines, PartyLines(party, "Client"), cols, rows, totals, note);
        return CommercialDocumentPdfRenderer.Render(model, TryLoadLogoBytes(cfg.SocieteLogoPath));
    }

    public async Task<byte[]> BuildAvoirFournisseurPdfAsync(AvoirFournisseur doc, DocumentPartyPdfInfo party, CancellationToken cancellationToken = default)
    {
        var cfg = await _settings.GetAsync(cancellationToken);
        var refs = await LoadProductRefsAsync(doc.Lignes.Select(l => l.ProduitId), cancellationToken);
        var totals = DocumentTotalsHelper.AvoirFournisseurTotals(doc.Lignes);
        var cols = BrColumns();
        var rows = new List<IReadOnlyList<string>>();
        foreach (var l in doc.Lignes)
        {
            var lht = l.Quantite * l.PrixUnitaireHT;
            var ttc = lht * (1 + l.TauxTVA / 100m);
            rows.Add([
                RefCell(refs, l.ProduitId),
                l.Designation,
                FmtQty(l.Quantite),
                FmtUnitPrice(l.PrixUnitaireHT),
                FmtTvaPct(l.TauxTVA),
                FmtMoney(lht),
                FmtMoney(ttc)
            ]);
        }

        var note = $"{doc.Motif}\nRetour marchandise : {(doc.RetourMarchandise ? "Oui" : "Non")}";
        var docLines = new List<PdfKeyValueLine>
        {
            new("N°", doc.Numero),
            new("Date", doc.Date.ToString("dd/MM/yyyy"))
        };

        var model = BaseModel(cfg, "AVOIR FOURNISSEUR", docLines, PartyLines(party, "Fournisseur"), cols, rows, totals, note);
        return CommercialDocumentPdfRenderer.Render(model, TryLoadLogoBytes(cfg.SocieteLogoPath));
    }

    private static CommercialDocumentPdfModel BaseModel(
        AppSettingsRow cfg,
        string kind,
        IReadOnlyList<PdfKeyValueLine> docLines,
        IReadOnlyList<PdfKeyValueLine> partyLines,
        IReadOnlyList<PdfTableColumn> columns,
        List<IReadOnlyList<string>> rows,
        (decimal ht, decimal tva, decimal ttc) totals,
        string? note,
        bool showTaxAndTtcInTotalsBox = true)
    {
        var qtyCol = FindQtyColumnIndex(columns);
        var refCol = FindRefColumnIndex(columns);
        decimal sumQty = 0;
        var refCount = 0;
        var qtyParse = CultureInfo.GetCultureInfo("fr-FR");
        foreach (var r in rows)
        {
            if (qtyCol >= 0 && qtyCol < r.Count && decimal.TryParse(r[qtyCol], NumberStyles.Any, qtyParse, out var q))
                sumQty += q;
            if (refCol >= 0 && refCol < r.Count && !string.IsNullOrWhiteSpace(r[refCol]) && r[refCol] != "—")
                refCount++;
        }

        if (refCount == 0)
            refCount = rows.Count;

        int leadingSpan;
        string[] summaryValues;
        if (qtyCol > 0 && qtyCol < columns.Count)
        {
            leadingSpan = qtyCol;
            summaryValues = new string[columns.Count - qtyCol];
            for (var i = 0; i < summaryValues.Length; i++)
                summaryValues[i] = i == 0 ? FmtQty(sumQty) : "";
        }
        else
        {
            leadingSpan = columns.Count;
            summaryValues = [];
        }

        var currencyWord = cfg.Devise.ToUpperInvariant() switch
        {
            "MAD" => "dirhams",
            "EUR" => "euros",
            "USD" => "dollars",
            _ => cfg.Devise
        };
        var amountForWords = showTaxAndTtcInTotalsBox ? totals.ttc : totals.ht;
        var amountWords = cfg.UiLanguage.Equals("ar", StringComparison.OrdinalIgnoreCase)
            ? MoneyFrenchWords.FormatArabicFallback(amountForWords, cfg.Devise)
            : MoneyFrenchWords.Format(amountForWords, currencyWord);

        return new CommercialDocumentPdfModel
        {
            CompanyName = cfg.SocieteNom,
            DocumentKindLabel = kind,
            DocumentInfoLines = docLines,
            PartyInfoLines = partyLines,
            Columns = columns,
            Rows = rows,
            SummaryRow = rows.Count > 0
                ? new PdfTableSummaryRow
                {
                    LeadingSpan = leadingSpan,
                    Label = $"Total : {refCount} référence(s)",
                    Values = summaryValues
                }
                : null,
            TotalHt = totals.ht,
            TotalTva = totals.tva,
            TotalTtc = totals.ttc,
            Devise = cfg.Devise,
            AmountInWords = amountWords,
            Note = note,
            FooterLines = BuildFooterLines(cfg),
            ShowTaxAndTtcInTotalsBox = showTaxAndTtcInTotalsBox
        };
    }

    private static int FindQtyColumnIndex(IReadOnlyList<PdfTableColumn> columns)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            if (columns[i].Header.Contains("livr", StringComparison.OrdinalIgnoreCase))
                return i;
        }

        for (var i = 0; i < columns.Count; i++)
        {
            var h = columns[i].Header.ToLowerInvariant();
            if (h.Contains("qté") || h.Contains("qte"))
                return i;
        }

        return -1;
    }

    private static int FindRefColumnIndex(IReadOnlyList<PdfTableColumn> columns)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            var h = columns[i].Header.Trim();
            if (h.StartsWith("Réf", StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private (List<PdfTableColumn> Columns, List<IReadOnlyList<string>> Rows) BuildStandardPdfTable(
        DocumentLineColumnVisibility visibility,
        bool supportsLineRemise,
        string qtyHeader,
        IReadOnlyList<StandardPdfLine> lines)
    {
        var v = supportsLineRemise ? visibility : visibility with { ShowRemise = false };
        var columns = BuildStandardColumnList(v, qtyHeader);
        if (columns.Count == 0)
            return BuildStandardPdfTable(DocumentLineColumnVisibility.AllVisible, supportsLineRemise, qtyHeader, lines);

        var rows = new List<IReadOnlyList<string>>(lines.Count);
        foreach (var line in lines)
            rows.Add(BuildStandardDataRow(v, line));

        return (columns, rows);
    }

    private static List<PdfTableColumn> BuildStandardColumnList(DocumentLineColumnVisibility v, string qtyHeader)
    {
        var columns = new List<PdfTableColumn>();
        if (v.ShowReference)
            columns.Add(new PdfTableColumn("Réf.", 0.9f));
        if (v.ShowDesignation)
            columns.Add(new PdfTableColumn("Désignation", 2.2f));
        if (v.ShowQuantite)
            columns.Add(new PdfTableColumn(qtyHeader, 0.75f, PdfTextAlignment.End));
        if (v.ShowConditionnement)
            columns.Add(new PdfTableColumn("Unité", 0.65f));
        if (v.ShowPuHt)
            columns.Add(new PdfTableColumn("PU HT", 0.85f, PdfTextAlignment.End));
        if (v.ShowTva)
            columns.Add(new PdfTableColumn("TVA %", 0.55f, PdfTextAlignment.End));
        if (v.ShowRemise)
            columns.Add(new PdfTableColumn("Rem. %", 0.55f, PdfTextAlignment.End));
        if (v.ShowMontantHt)
            columns.Add(new PdfTableColumn("Mnt HT", 0.85f, PdfTextAlignment.End));
        if (v.ShowMontantTtc)
            columns.Add(new PdfTableColumn("Mnt TTC", 0.9f, PdfTextAlignment.End));
        return columns;
    }

    private static List<string> BuildStandardDataRow(DocumentLineColumnVisibility v, StandardPdfLine line)
    {
        var cells = new List<string>();
        if (v.ShowReference)
            cells.Add(line.Ref);
        if (v.ShowDesignation)
            cells.Add(line.Designation);
        if (v.ShowQuantite)
            cells.Add(line.Quantite);
        if (v.ShowConditionnement)
            cells.Add(line.Unite);
        if (v.ShowPuHt)
            cells.Add(line.PuHt);
        if (v.ShowTva)
            cells.Add(line.Tva);
        if (v.ShowRemise)
            cells.Add(line.Remise);
        if (v.ShowMontantHt)
            cells.Add(line.MntHt);
        if (v.ShowMontantTtc)
            cells.Add(line.MntTtc);
        return cells;
    }

    private readonly record struct StandardPdfLine(
        string Ref,
        string Designation,
        string Quantite,
        string Unite,
        string PuHt,
        string Tva,
        string Remise,
        string MntHt,
        string MntTtc);

    /// <summary>Bon de livraison line cells (no Unité on entity; column visibility follows bon_livraison prefs).</summary>
    private readonly record struct BlPdfLine(
        string Ref,
        string Designation,
        string QteCmd,
        string QteLivr,
        string PuHt,
        string Tva,
        string MntHt,
        string MntTtc);

    private (List<PdfTableColumn> Columns, List<IReadOnlyList<string>> Rows) BuildBlPdfTable(
        DocumentLineColumnVisibility visibility,
        IReadOnlyList<BlPdfLine> lines)
    {
        var columns = BuildBlColumnList(visibility);
        if (columns.Count == 0)
            return BuildBlPdfTable(DocumentLineColumnVisibility.AllVisible, lines);

        var rows = new List<IReadOnlyList<string>>(lines.Count);
        foreach (var line in lines)
            rows.Add(BuildBlDataRow(visibility, line));

        return (columns, rows);
    }

    private static List<PdfTableColumn> BuildBlColumnList(DocumentLineColumnVisibility v)
    {
        var columns = new List<PdfTableColumn>();
        if (v.ShowReference)
            columns.Add(new PdfTableColumn("Réf.", 0.9f));
        if (v.ShowDesignation)
            columns.Add(new PdfTableColumn("Désignation", 2f));
        if (v.ShowQuantite)
        {
            columns.Add(new PdfTableColumn("Qté cmd.", 0.7f, PdfTextAlignment.End));
            columns.Add(new PdfTableColumn("Qté livr.", 0.75f, PdfTextAlignment.End));
        }

        if (v.ShowPuHt)
            columns.Add(new PdfTableColumn("PU HT", 0.85f, PdfTextAlignment.End));
        if (v.ShowTva)
            columns.Add(new PdfTableColumn("TVA %", 0.55f, PdfTextAlignment.End));
        if (v.ShowMontantHt)
            columns.Add(new PdfTableColumn("Mnt HT", 0.85f, PdfTextAlignment.End));
        if (v.ShowMontantTtc)
            columns.Add(new PdfTableColumn("Mnt TTC", 0.9f, PdfTextAlignment.End));
        return columns;
    }

    private static List<string> BuildBlDataRow(DocumentLineColumnVisibility v, BlPdfLine line)
    {
        var cells = new List<string>();
        if (v.ShowReference)
            cells.Add(line.Ref);
        if (v.ShowDesignation)
            cells.Add(line.Designation);
        if (v.ShowQuantite)
        {
            cells.Add(line.QteCmd);
            cells.Add(line.QteLivr);
        }

        if (v.ShowPuHt)
            cells.Add(line.PuHt);
        if (v.ShowTva)
            cells.Add(line.Tva);
        if (v.ShowMontantHt)
            cells.Add(line.MntHt);
        if (v.ShowMontantTtc)
            cells.Add(line.MntTtc);
        return cells;
    }

    private static IReadOnlyList<PdfTableColumn> BrColumns() =>
    [
        new("Réf.", 0.9f),
        new("Désignation", 2.2f),
        new("Qté", 0.75f, PdfTextAlignment.End),
        new("PU HT", 0.85f, PdfTextAlignment.End),
        new("TVA %", 0.55f, PdfTextAlignment.End),
        new("Mnt HT", 0.85f, PdfTextAlignment.End),
        new("Mnt TTC", 0.9f, PdfTextAlignment.End)
    ];

    private static List<PdfKeyValueLine> PartyLines(DocumentPartyPdfInfo p, string roleLabel)
    {
        var list = new List<PdfKeyValueLine> { new(roleLabel, p.Nom) };
        if (!string.IsNullOrWhiteSpace(p.Ice))
            list.Add(new("ICE", p.Ice));
        if (!string.IsNullOrWhiteSpace(p.Adresse))
            list.Add(new("Adresse", p.Adresse));
        return list;
    }

    private static string RefCell(Dictionary<int, string> refs, int produitId) =>
        produitId > 0 && refs.TryGetValue(produitId, out var r) && !string.IsNullOrWhiteSpace(r) ? r : "—";

    private static string? SummarizePaiements(IReadOnlyList<Paiement>? paiements)
    {
        if (paiements == null || paiements.Count == 0) return null;
        var total = paiements.Sum(p => p.Montant);
        var modes = string.Join(", ", paiements.Select(p => ModeFr(p.Mode)).Distinct());
        return $"{total:N2} — {modes}";
    }

    private static string ModeFr(ModePaiement m) => m switch
    {
        ModePaiement.Credit => "Crédit",
        ModePaiement.Cheque => "Chèque",
        ModePaiement.Especes => "Espèces",
        ModePaiement.TPE => "TPE",
        ModePaiement.Virement => "Virement",
        ModePaiement.Effet => "Effet",
        _ => m.ToString()
    };

    private async Task<Dictionary<int, string>> LoadProductRefsAsync(IEnumerable<int> productIds, CancellationToken cancellationToken)
    {
        var ids = productIds.Where(x => x > 0).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, string>();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Produits.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Reference ?? "", cancellationToken);
    }

    private static IReadOnlyList<string> BuildFooterLines(AppSettingsRow cfg)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(cfg.SocieteAdresse))
            lines.Add(cfg.SocieteAdresse.Trim());
        if (!string.IsNullOrWhiteSpace(cfg.SocieteICE))
            lines.Add($"ICE : {cfg.SocieteICE.Trim()}");

        if (!string.IsNullOrWhiteSpace(cfg.SocieteMentionsLegales))
        {
            foreach (var part in cfg.SocieteMentionsLegales.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                lines.Add(part);
        }

        return lines;
    }

    private static byte[]? TryLoadLogoBytes(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            if (!File.Exists(path)) return null;
            return File.ReadAllBytes(path);
        }
        catch
        {
            return null;
        }
    }
}
