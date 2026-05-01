using GestionCommerciale.Modules.Facturation.Models;
using GestionCommerciale.Shared.Database;
using GestionCommerciale.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace GestionCommerciale.Modules.Facturation.Services;

public sealed class FactureWorkflowService : IFactureWorkflowService
{
    private const decimal PaiementTtcTolerance = 0.02m;

    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public FactureWorkflowService(IDbContextFactory<AppDbContext> dbFactory) => _dbFactory = dbFactory;

    private static void EnsureTotalPaiementsNotOverTtc(decimal ttc, decimal totalPaiements)
    {
        if (totalPaiements > ttc + PaiementTtcTolerance)
        {
            throw new InvalidOperationException(
                $"La somme des paiements ({totalPaiements:N2} TTC) ne peut pas dépasser le total de la facture ({ttc:N2} TTC).");
        }
    }

    public async Task AddPaiementAsync(int factureId, Paiement paiement, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var f = await db.Factures
            .Include(x => x.Lignes)
            .Include(x => x.Paiements)
            .FirstAsync(x => x.Id == factureId, cancellationToken);

        var (_, _, ttc) = DocumentTotalsHelper.FactureTotals(f.Lignes, f.RemiseGlobale);
        var totalApres = f.Paiements.Sum(p => p.Montant) + paiement.Montant;
        EnsureTotalPaiementsNotOverTtc(ttc, totalApres);

        paiement.FactureId = factureId;
        db.Paiements.Add(paiement);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePaiementAsync(int factureId, int paiementId, decimal montant, DateTime date, ModePaiement mode, string reference, CancellationToken cancellationToken = default)
    {
        if (montant <= 0)
            throw new InvalidOperationException("Le montant doit être supérieur à 0.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var f = await db.Factures
            .Include(x => x.Lignes)
            .Include(x => x.Paiements)
            .FirstAsync(x => x.Id == factureId, cancellationToken);

        var (_, _, ttc) = DocumentTotalsHelper.FactureTotals(f.Lignes, f.RemiseGlobale);
        var totalApres = f.Paiements.Where(x => x.Id != paiementId).Sum(x => x.Montant) + montant;
        EnsureTotalPaiementsNotOverTtc(ttc, totalApres);

        var p = await db.Paiements.FirstAsync(x => x.Id == paiementId && x.FactureId == factureId, cancellationToken);
        p.Montant = montant;
        p.Date = date;
        p.Mode = mode;
        p.Reference = reference;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeletePaiementAsync(int factureId, int paiementId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var p = await db.Paiements.FirstAsync(x => x.Id == paiementId && x.FactureId == factureId, cancellationToken);
        db.Paiements.Remove(p);
        await db.SaveChangesAsync(cancellationToken);
    }
}
