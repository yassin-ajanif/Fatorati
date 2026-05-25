using GestionCommerciale.Modules.Facturation.Models;
using GestionCommerciale.Modules.Reporting.ViewModels;
using GestionCommerciale.Modules.Stock.Models;
using GestionCommerciale.Modules.Tiers.Models;
using GestionCommerciale.Shared.Database;
using GestionCommerciale.Shared.Helpers;
using GestionCommerciale.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace GestionCommerciale.Modules.Reporting.Services;

public sealed class ReportService : IReportService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAppSettingsService _settings;
    private readonly ILocaleService _locale;

    public ReportService(
        IDbContextFactory<AppDbContext> dbFactory,
        IAppSettingsService settings,
        ILocaleService locale)
    {
        _dbFactory = dbFactory;
        _settings = settings;
        _locale = locale;
    }

    public async Task<List<ReportSaleByProductRow>> GetSalesByProductAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        var dev = await GetDeviseAsync(ct);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var toEnd = to.Date.AddDays(1);

        var lignes = await db.FactureLignes.AsNoTracking()
            .Where(l => l.Facture!.Date >= from && l.Facture.Date < toEnd)
            .Select(l => new
            {
                l.ProduitId,
                l.Quantite,
                l.PrixUnitaireHT,
                l.Remise,
                l.TauxTVA,
                l.Designation
            })
            .ToListAsync(ct);

        var prodIds = lignes.Select(l => l.ProduitId).Distinct().ToList();
        var produits = await db.Produits.AsNoTracking()
            .Where(p => prodIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Reference, p.Designation, Categorie = p.Categorie != null ? p.Categorie.Nom : "" })
            .ToListAsync(ct);
        var prodMap = produits.ToDictionary(p => p.Id);

        var grouped = lignes
            .GroupBy(l => l.ProduitId)
            .Select(g =>
            {
                var p = prodMap.GetValueOrDefault(g.Key);
                var ht = g.Sum(l => DocumentTotalsHelper.LigneHT(l.Quantite, l.PrixUnitaireHT, l.Remise));
                var tva = g.Sum(l => DocumentTotalsHelper.LigneHT(l.Quantite, l.PrixUnitaireHT, l.Remise) * (l.TauxTVA / 100m));
                return new ReportSaleByProductRow(
                    p?.Reference ?? string.Empty,
                    p?.Designation ?? g.First().Designation,
                    p?.Categorie ?? string.Empty,
                    g.Sum(l => l.Quantite),
                    ht,
                    ht + tva,
                    dev);
            })
            .OrderByDescending(r => r.TotalTtc)
            .ToList();

        return grouped;
    }

    public async Task<List<ReportSaleByCustomerRow>> GetSalesByCustomerAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        var dev = await GetDeviseAsync(ct);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var toEnd = to.Date.AddDays(1);

        var factures = await db.Factures.AsNoTracking()
            .Where(f => f.Date >= from && f.Date < toEnd)
            .Select(f => new
            {
                f.Id,
                f.ClientId,
                f.RemiseGlobale,
                Lignes = f.Lignes!.Select(l => new
                {
                    l.Quantite, l.PrixUnitaireHT, l.Remise, l.TauxTVA
                }).ToList()
            })
            .ToListAsync(ct);

        var clientIds = factures.Select(f => f.ClientId).Distinct().ToList();
        var clients = await db.Tiers.AsNoTracking()
            .Where(t => clientIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Nom, t.ICE, t.Ville })
            .ToListAsync(ct);
        var clientMap = clients.ToDictionary(c => c.Id);

        var grouped = factures
            .GroupBy(f => f.ClientId)
            .Select(g =>
            {
                var c = clientMap.GetValueOrDefault(g.Key);
                var totaux = g.Select(f =>
                {
                    var lignes = f.Lignes.Select(l => new FactureLigne
                    {
                        Quantite = l.Quantite,
                        PrixUnitaireHT = l.PrixUnitaireHT,
                        Remise = l.Remise,
                        TauxTVA = l.TauxTVA
                    }).ToList();
                    return DocumentTotalsHelper.FactureTotals(lignes, f.RemiseGlobale);
                });
                return new ReportSaleByCustomerRow(
                    c?.Nom ?? string.Empty,
                    c?.ICE ?? string.Empty,
                    c?.Ville ?? string.Empty,
                    g.Count(),
                    totaux.Sum(t => t.ht),
                    totaux.Sum(t => t.ttc),
                    dev);
            })
            .OrderByDescending(r => r.TotalTtc)
            .ToList();

        return grouped;
    }

    public async Task<List<ReportRefundRow>> GetRefundsAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        var dev = await GetDeviseAsync(ct);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var toEnd = to.Date.AddDays(1);

        var avoirs = await db.Avoirs.AsNoTracking()
            .Where(a => a.Date >= from && a.Date < toEnd)
            .OrderByDescending(a => a.Date)
            .Select(a => new
            {
                a.Id,
                a.Numero,
                a.Date,
                a.ClientId,
                a.Motif,
                a.RetourMarchandise,
                Lignes = a.Lignes!.Select(l => new
                {
                    l.Quantite, l.PrixUnitaireHT, l.TauxTVA
                }).ToList()
            })
            .ToListAsync(ct);

        var clientIds = avoirs.Select(a => a.ClientId).Distinct().ToList();
        var clients = await db.Tiers.AsNoTracking()
            .Where(t => clientIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Nom })
            .ToListAsync(ct);
        var clientMap = clients.ToDictionary(c => c.Id);

        return avoirs.Select(a =>
        {
            var lignes = a.Lignes.Select(l => new AvoirLigne
            {
                Quantite = l.Quantite,
                PrixUnitaireHT = l.PrixUnitaireHT,
                TauxTVA = l.TauxTVA
            }).ToList();
            return new ReportRefundRow(
                a.Numero ?? string.Empty,
                a.Date,
                clientMap.GetValueOrDefault(a.ClientId)?.Nom ?? string.Empty,
                a.Motif ?? string.Empty,
                a.RetourMarchandise,
                DocumentTotalsHelper.AvoirTotals(lignes).ttc,
                dev);
        }).ToList();
    }

    public async Task<List<ReportDailySaleRow>> GetDailySalesAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        var dev = await GetDeviseAsync(ct);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var toEnd = to.Date.AddDays(1);

        var factures = await db.Factures.AsNoTracking()
            .Where(f => f.Date >= from && f.Date < toEnd)
            .OrderBy(f => f.Date)
            .Select(f => new
            {
                f.Date,
                f.RemiseGlobale,
                Lignes = f.Lignes!.Select(l => new
                {
                    l.Quantite, l.PrixUnitaireHT, l.Remise, l.TauxTVA
                }).ToList()
            })
            .ToListAsync(ct);

        var grouped = factures
            .GroupBy(f => f.Date.Date)
            .Select(g =>
            {
                var totaux = g.Select(f =>
                {
                    var lignes = f.Lignes.Select(l => new FactureLigne
                    {
                        Quantite = l.Quantite,
                        PrixUnitaireHT = l.PrixUnitaireHT,
                        Remise = l.Remise,
                        TauxTVA = l.TauxTVA
                    }).ToList();
                    return DocumentTotalsHelper.FactureTotals(lignes, f.RemiseGlobale);
                });
                return new ReportDailySaleRow(
                    g.Key,
                    g.Count(),
                    totaux.Sum(t => t.ht),
                    totaux.Sum(t => t.tva),
                    totaux.Sum(t => t.ttc),
                    dev);
            })
            .OrderByDescending(r => r.Date)
            .ToList();

        return grouped;
    }

    public async Task<List<ReportUnpaidRow>> GetUnpaidSalesAsync(CancellationToken ct = default)
    {
        var dev = await GetDeviseAsync(ct);
        var now = DateTime.Today;
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var unpaid = await db.Factures.AsNoTracking()
            .Where(f => !f.EstPayee)
            .OrderBy(f => f.DateEcheance)
            .Take(200)
            .Select(f => new
            {
                f.Numero,
                f.DateEcheance,
                f.RemiseGlobale,
                Lignes = f.Lignes!.Select(l => new
                {
                    l.Quantite, l.PrixUnitaireHT, l.Remise, l.TauxTVA
                }).ToList(),
                Paiements = f.Paiements!.Select(p => p.Montant).ToList()
            })
            .ToListAsync(ct);

        var rows = new List<ReportUnpaidRow>();
        foreach (var f in unpaid)
        {
            var lignes = f.Lignes.Select(l => new FactureLigne
            {
                Quantite = l.Quantite,
                PrixUnitaireHT = l.PrixUnitaireHT,
                Remise = l.Remise,
                TauxTVA = l.TauxTVA
            }).ToList();
            var (_, _, ttc) = DocumentTotalsHelper.FactureTotals(lignes, f.RemiseGlobale);
            var paye = f.Paiements.Sum();
            var reste = ttc - paye;
            if (reste <= 0.01m) continue;

            var due = f.DateEcheance.Date;
            var daysFromDue = (now - due).Days;
            string dueStatus;
            var isOverdue = daysFromDue > 0;
            var isDueSoon = false;
            if (daysFromDue > 0)
                dueStatus = _locale.Tf("Report_UnpaidOverdueFmt", daysFromDue.ToString());
            else if (daysFromDue == 0)
                dueStatus = _locale.T("Report_UnpaidDueToday");
            else
            {
                var until = -daysFromDue;
                dueStatus = _locale.Tf("Report_UnpaidDueInFmt", until.ToString());
                if (until <= 7)
                    isDueSoon = true;
            }

            rows.Add(new ReportUnpaidRow(
                f.Numero ?? string.Empty,
                CurrencyHelper.Format(reste, dev),
                f.DateEcheance.ToString("d"),
                dueStatus,
                isOverdue,
                isDueSoon));
        }

        return rows;
    }

    public async Task<List<ReportStockMovementRow>> GetStockMovementsAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var toEnd = to.Date.AddDays(1);

        var mouvements = await db.MouvementsStock.AsNoTracking()
            .Where(m => m.CreatedAt >= from && m.CreatedAt < toEnd)
            .Include(m => m.Produit)
            .OrderByDescending(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Take(500)
            .ToListAsync(ct);

        return mouvements.Select(m =>
        {
            var typeStr = m.Type switch
            {
                TypeMouvement.Entree => _locale.T("TypeMvt_Entree"),
                TypeMouvement.Sortie => _locale.T("TypeMvt_Sortie"),
                TypeMouvement.Ajustement => _locale.T("TypeMvt_Ajustement"),
                _ => m.Type.ToString()
            };
            return new ReportStockMovementRow(
                m.CreatedAt,
                m.Produit?.Reference ?? string.Empty,
                m.Produit?.Designation ?? string.Empty,
                typeStr,
                m.Quantite,
                m.OrigineType,
                m.StockApres);
        }).ToList();
    }

    private async Task<string> GetDeviseAsync(CancellationToken ct = default)
    {
        var cfg = await _settings.GetAsync(ct);
        return string.IsNullOrWhiteSpace(cfg.Devise) ? "MAD" : cfg.Devise!;
    }
}
