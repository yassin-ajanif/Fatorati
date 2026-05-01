using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionCommerciale.Modules.Auth.Services;
using GestionCommerciale.Modules.Commande.Models;
using GestionCommerciale.Modules.Facturation.Models;
using GestionCommerciale.Modules.Livraison.Models;
using GestionCommerciale.Modules.Reception.Models;
using GestionCommerciale.Modules.Stock;
using GestionCommerciale.Shared.Database;
using GestionCommerciale.Shared.Helpers;
using GestionCommerciale.Shared.Services;
using GestionCommerciale.Shared.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace GestionCommerciale.Modules.Reporting.ViewModels;

public partial class ReportingViewModel : BaseViewModel
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IDialogService _dialog;
    private readonly IAppSettingsService _settings;
    private readonly ICurrentUserSession _session;
    private readonly ILocaleService _locale;

    public ReportingViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        IDialogService dialog,
        IAppSettingsService settings,
        ICurrentUserSession session,
        ILocaleService locale)
    {
        _dbFactory = dbFactory;
        _dialog = dialog;
        _settings = settings;
        _session = session;
        _locale = locale;
        _locale.CultureApplied += (_, _) => RefreshReportingUi();
        RefreshReportingUi();
        Title = _locale.T("Report_Title");
    }

    [ObservableProperty] private string _btnRefresh = string.Empty;
    [ObservableProperty] private string _btnExportStockCsv = string.Empty;
    [ObservableProperty] private string _lblCa = string.Empty;
    [ObservableProperty] private string _lblCaDelta = string.Empty;
    [ObservableProperty] private string _lblKpiStrip = string.Empty;
    [ObservableProperty] private string _lblTopClients = string.Empty;
    [ObservableProperty] private string _lblTopProducts = string.Empty;
    [ObservableProperty] private string _lblStockAlerts = string.Empty;
    [ObservableProperty] private string _lblUnpaid = string.Empty;
    [ObservableProperty] private string _lineCaCurrent = string.Empty;
    [ObservableProperty] private string _lineCaPrev = string.Empty;
    [ObservableProperty] private string _lineCaDelta = string.Empty;
    [ObservableProperty] private string _lblLoading = string.Empty;

    [ObservableProperty] private string _caMoisCourant = string.Empty;
    [ObservableProperty] private string _caMoisPrecedent = string.Empty;

    [ObservableProperty] private string _kpiDevis30 = string.Empty;
    [ObservableProperty] private string _kpiDevisExpire = string.Empty;
    [ObservableProperty] private string _kpiBlMonth = string.Empty;
    [ObservableProperty] private string _kpiBc = string.Empty;
    [ObservableProperty] private string _kpiBrMonth = string.Empty;
    [ObservableProperty] private string _kpiEncours = string.Empty;
    [ObservableProperty] private string _kpiAvoirYtd = string.Empty;
    [ObservableProperty] private string _kpiStock = string.Empty;

    [ObservableProperty] private bool _showEmptyTopClients;
    [ObservableProperty] private bool _showEmptyTopProducts;
    [ObservableProperty] private bool _showEmptyStock;
    [ObservableProperty] private bool _showEmptyUnpaid;

    [ObservableProperty] private string _emptyMessageTopClients = string.Empty;
    [ObservableProperty] private string _emptyMessageTopProducts = string.Empty;
    [ObservableProperty] private string _emptyMessageStock = string.Empty;
    [ObservableProperty] private string _emptyMessageUnpaid = string.Empty;

    public ObservableCollection<ReportRankRow> TopClients { get; } = [];
    public ObservableCollection<ReportRankRow> TopProduits { get; } = [];
    public ObservableCollection<ReportStockAlertRow> StockAlertes { get; } = [];
    public ObservableCollection<ReportUnpaidRow> FacturesImpayees { get; } = [];

    private void RefreshReportingUi()
    {
        Title = _locale.T("Report_Title");
        BtnRefresh = _locale.T("Btn_Refresh");
        BtnExportStockCsv = _locale.T("Btn_ExportStockCsv");
        LblLoading = _locale.T("Report_Loading");
        LblCa = _locale.T("Report_LblCa");
        LblCaDelta = _locale.T("Report_LblCaDelta");
        LblKpiStrip = _locale.T("Report_LblKpiStrip");
        LblTopClients = _locale.T("Report_LblTopClients");
        LblTopProducts = _locale.T("Report_LblTopProducts");
        LblStockAlerts = _locale.T("Report_LblStockAlerts");
        LblUnpaid = _locale.T("Report_LblUnpaid");
        LineCaCurrent = _locale.Tf("Report_FmtCurrentMonth", CaMoisCourant);
        LineCaPrev = _locale.Tf("Report_FmtPrevMonth", CaMoisPrecedent);
        EmptyMessageTopClients = _locale.T("Report_EmptyTopClients");
        EmptyMessageTopProducts = _locale.T("Report_EmptyTopProducts");
        EmptyMessageStock = _locale.T("Report_EmptyStock");
        EmptyMessageUnpaid = _locale.T("Report_EmptyUnpaid");
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (!_session.CanAccessReporting)
        {
            await _dialog.ShowErrorAsync(_locale.T("Report_Title"), _locale.T("Report_ErrDenied"), cancellationToken);
            return;
        }

        IsBusy = true;
        try
        {
            try
            {
            var cfg = await _settings.GetAsync(cancellationToken);
            var dev = string.IsNullOrWhiteSpace(cfg.Devise) ? "MAD" : cfg.Devise!;
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var now = DateTime.Today;
            var startCur = new DateTime(now.Year, now.Month, 1);
            var startPrev = startCur.AddMonths(-1);
            var endCur = startCur.AddMonths(1);
            var endPrev = startCur;
            var yearStart = new DateTime(now.Year, 1, 1);
            var since30 = now.AddDays(-30);
            var expireUntil = now.AddDays(14);

            var facCur = await db.Factures.AsNoTracking()
                .Where(f => f.Date >= startCur && f.Date < endCur
                    && f.Statut != StatutFacture.Annulee
                    && f.Statut != StatutFacture.Brouillon)
                .Include(f => f.Lignes)
                .ToListAsync(cancellationToken);
            var caCur = facCur.Sum(f => DocumentTotalsHelper.FactureTotals(f.Lignes ?? [], f.RemiseGlobale).ttc);

            var facPrev = await db.Factures.AsNoTracking()
                .Where(f => f.Date >= startPrev && f.Date < endPrev
                    && f.Statut != StatutFacture.Annulee
                    && f.Statut != StatutFacture.Brouillon)
                .Include(f => f.Lignes)
                .ToListAsync(cancellationToken);
            var caPrev = facPrev.Sum(f => DocumentTotalsHelper.FactureTotals(f.Lignes ?? [], f.RemiseGlobale).ttc);

            CaMoisCourant = CurrencyHelper.Format(caCur, dev);
            CaMoisPrecedent = CurrencyHelper.Format(caPrev, dev);
            LineCaCurrent = _locale.Tf("Report_FmtCurrentMonth", CaMoisCourant);
            LineCaPrev = _locale.Tf("Report_FmtPrevMonth", CaMoisPrecedent);

            if (Math.Abs(caPrev) < 0.01m && Math.Abs(caCur) < 0.01m)
                LineCaDelta = _locale.T("Report_FmtCaDeltaZero");
            else if (Math.Abs(caPrev) < 0.01m)
                LineCaDelta = _locale.T("Report_FmtCaDeltaFromZero");
            else
            {
                var diff = caCur - caPrev;
                var pct = (double)(diff / caPrev * 100m);
                LineCaDelta = _locale.Tf("Report_FmtCaDeltaFmt",
                    CurrencyHelper.Format(diff, dev),
                    pct.ToString("F1", CultureInfo.CurrentCulture));
            }

            var devis30 = await db.Devis.AsNoTracking().CountAsync(d => d.Date >= since30, cancellationToken);
            var devisExpire = await db.Devis.AsNoTracking().CountAsync(
                d => d.DateValidite >= now && d.DateValidite <= expireUntil,
                cancellationToken);
            var blMonth = await db.BonsLivraison.AsNoTracking().CountAsync(
                b => b.Date >= startCur && b.Date < endCur && b.Statut != StatutBL.Brouillon,
                cancellationToken);
            var bcDraft = await db.BonsCommande.AsNoTracking().CountAsync(b => b.Statut == StatutBC.Brouillon, cancellationToken);
            var bcValide = await db.BonsCommande.AsNoTracking().CountAsync(b => b.Statut == StatutBC.Valide, cancellationToken);
            var brMonth = await db.BonsReception.AsNoTracking().CountAsync(
                b => b.Date >= startCur && b.Date < endCur && b.Statut == StatutBR.Valide,
                cancellationToken);

            KpiDevis30 = _locale.Tf("Report_KpiDevis30", devis30.ToString(CultureInfo.CurrentCulture));
            KpiDevisExpire = _locale.Tf("Report_KpiDevisExpire", devisExpire.ToString(CultureInfo.CurrentCulture));
            KpiBlMonth = _locale.Tf("Report_KpiBlMonth", blMonth.ToString(CultureInfo.CurrentCulture));
            KpiBc = _locale.Tf("Report_KpiBc",
                bcDraft.ToString(CultureInfo.CurrentCulture),
                bcValide.ToString(CultureInfo.CurrentCulture));
            KpiBrMonth = _locale.Tf("Report_KpiBrMonth", brMonth.ToString(CultureInfo.CurrentCulture));

            var factsYear = await db.Factures.AsNoTracking()
                .Where(f => f.Date >= startCur.AddMonths(-11) && f.Statut != StatutFacture.Annulee)
                .Include(f => f.Lignes)
                .ToListAsync(cancellationToken);
            var topClients = factsYear
                .GroupBy(f => f.ClientId)
                .Select(g => new { ClientId = g.Key, Total = g.Sum(f => DocumentTotalsHelper.FactureTotals(f.Lignes ?? [], f.RemiseGlobale).ttc) })
                .OrderByDescending(x => x.Total)
                .Take(5)
                .ToList();

            var maxClient = topClients.Count > 0 ? topClients.Max(x => x.Total) : 0m;

            TopClients.Clear();
            foreach (var x in topClients)
            {
                var nom = await db.Tiers.AsNoTracking().Where(t => t.Id == x.ClientId).Select(t => t.Nom).FirstOrDefaultAsync(cancellationToken);
                var share = maxClient > 0 ? (double)(x.Total / maxClient) : 0;
                TopClients.Add(new ReportRankRow(
                    nom ?? string.Empty,
                    CurrencyHelper.Format(x.Total, dev),
                    share));
            }

            ShowEmptyTopClients = TopClients.Count == 0;

            var blSince = startCur.AddMonths(-11);
            var blLignes = await (
                from l in db.BonLivraisonLignes.AsNoTracking()
                join b in db.BonsLivraison.AsNoTracking() on l.BLId equals b.Id
                where b.Date >= blSince && b.Statut != StatutBL.Brouillon
                select l
            ).ToListAsync(cancellationToken);
            var topProd = blLignes
                .GroupBy(l => l.ProduitId)
                .Select(g => new { ProduitId = g.Key, Qty = g.Sum(x => x.QuantiteLivree) })
                .OrderByDescending(x => x.Qty)
                .Take(5)
                .ToList();

            var maxQty = topProd.Count > 0 ? topProd.Max(x => x.Qty) : 0m;

            TopProduits.Clear();
            foreach (var x in topProd)
            {
                var nom = await db.Produits.AsNoTracking().Where(p => p.Id == x.ProduitId).Select(p => p.Designation).FirstOrDefaultAsync(cancellationToken);
                var share = maxQty > 0 ? (double)(x.Qty / maxQty) : 0;
                TopProduits.Add(new ReportRankRow(
                    nom ?? string.Empty,
                    x.Qty.ToString("N2", CultureInfo.CurrentCulture),
                    share));
            }

            ShowEmptyTopProducts = TopProduits.Count == 0;

            StockAlertes.Clear();
            var alerts = await db.Produits.AsNoTracking()
                .Where(p => p.Actif && p.StockMinimum > 0 && p.StockActuel < p.StockMinimum)
                .SelectForListWithoutImageData()
                .Take(100)
                .ToListAsync(cancellationToken);
            foreach (var p in alerts)
            {
                StockAlertes.Add(new ReportStockAlertRow(
                    p.Reference,
                    _locale.Tf("Report_FmtStockDetail",
                        p.StockActuel.ToString("N2", CultureInfo.CurrentCulture),
                        p.StockMinimum.ToString("N2", CultureInfo.CurrentCulture))));
            }

            ShowEmptyStock = StockAlertes.Count == 0;

            var actifs = await db.Produits.AsNoTracking().CountAsync(p => p.Actif, cancellationToken);
            var sousMin = await db.Produits.AsNoTracking().CountAsync(
                p => p.Actif && p.StockMinimum > 0 && p.StockActuel < p.StockMinimum,
                cancellationToken);
            var pctSous = actifs > 0 ? (double)sousMin / actifs * 100.0 : 0;
            KpiStock = _locale.Tf("Report_KpiStock",
                actifs.ToString(CultureInfo.CurrentCulture),
                sousMin.ToString(CultureInfo.CurrentCulture),
                pctSous.ToString("F0", CultureInfo.CurrentCulture));

            FacturesImpayees.Clear();
            var unpaid = await db.Factures.AsNoTracking()
                .Where(f => f.Statut != StatutFacture.Annulee && f.Statut != StatutFacture.Payee)
                .Include(f => f.Lignes)
                .Include(f => f.Paiements)
                .OrderBy(f => f.DateEcheance)
                .Take(200)
                .ToListAsync(cancellationToken);

            decimal encoursTotal = 0;
            var encoursCount = 0;
            foreach (var f in unpaid)
            {
                var (_, _, ttc) = DocumentTotalsHelper.FactureTotals(f.Lignes ?? [], f.RemiseGlobale);
                var paye = (f.Paiements ?? []).Sum(p => p.Montant);
                var reste = ttc - paye;
                if (reste <= 0.01m) continue;

                encoursTotal += reste;
                encoursCount++;

                var due = f.DateEcheance.Date;
                var daysFromDue = (now - due).Days;
                string dueStatus;
                var isOverdue = daysFromDue > 0;
                var isDueSoon = false;
                if (daysFromDue > 0)
                    dueStatus = _locale.Tf("Report_UnpaidOverdueFmt", daysFromDue.ToString(CultureInfo.CurrentCulture));
                else if (daysFromDue == 0)
                    dueStatus = _locale.T("Report_UnpaidDueToday");
                else
                {
                    var until = -daysFromDue;
                    dueStatus = _locale.Tf("Report_UnpaidDueInFmt", until.ToString(CultureInfo.CurrentCulture));
                    if (until <= 7)
                        isDueSoon = true;
                }

                FacturesImpayees.Add(new ReportUnpaidRow(
                    f.Numero,
                    CurrencyHelper.Format(reste, dev),
                    f.DateEcheance.ToString("d", CultureInfo.CurrentCulture),
                    dueStatus,
                    isOverdue,
                    isDueSoon));
            }

            KpiEncours = _locale.Tf("Report_KpiEncours",
                CurrencyHelper.Format(encoursTotal, dev),
                encoursCount.ToString(CultureInfo.CurrentCulture));

            var avoirsYtd = await db.Avoirs.AsNoTracking()
                .Where(a => a.Date >= yearStart)
                .Include(a => a.Lignes)
                .ToListAsync(cancellationToken);
            var avoirTtc = avoirsYtd.Sum(a => DocumentTotalsHelper.AvoirTotals(a.Lignes ?? []).ttc);
            KpiAvoirYtd = _locale.Tf("Report_KpiAvoirYtd", CurrencyHelper.Format(avoirTtc, dev));

            ShowEmptyUnpaid = FacturesImpayees.Count == 0;
            }
            catch (Exception ex)
            {
                await _dialog.ShowErrorAsync(_locale.T("Report_Title"), ex.Message, cancellationToken);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportStockCsvAsync(CancellationToken cancellationToken)
    {
        var path = await _dialog.PickSaveFileAsync(_locale.T("Report_ExportStockCsv"), "stock.csv", new[] { "*.csv" }, cancellationToken);
        if (path == null) return;
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var list = await db.Produits.AsNoTracking().SelectForListWithoutImageData().ToListAsync(cancellationToken);
        await using var w = new StreamWriter(path);
        await w.WriteLineAsync("Reference;Designation;StockActuel;StockMinimum");
        foreach (var p in list)
            await w.WriteLineAsync($"{p.Reference};{p.Designation};{p.StockActuel.ToString(CultureInfo.InvariantCulture)};{p.StockMinimum.ToString(CultureInfo.InvariantCulture)}");
        await _dialog.ShowInfoAsync(_locale.T("Export_Csv"), _locale.T("Export_Done"), cancellationToken);
    }
}
