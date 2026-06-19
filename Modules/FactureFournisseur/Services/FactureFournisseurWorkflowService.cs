using GestionCommerciale.Modules.Facturation.Models;
using GestionCommerciale.Modules.FactureFournisseur.Models;
using GestionCommerciale.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace GestionCommerciale.Modules.FactureFournisseur.Services;

public sealed class FactureFournisseurWorkflowService : IFactureFournisseurWorkflowService
{
    private const decimal PaiementTtcTolerance = 0.02m;

    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public FactureFournisseurWorkflowService(IDbContextFactory<AppDbContext> dbFactory) => _dbFactory = dbFactory;

    private static void EnsureTotalPaiementsNotOverTtc(decimal ttc, decimal totalPaiements)
    {
        if (totalPaiements > ttc + PaiementTtcTolerance)
        {
            throw new InvalidOperationException(
                $"La somme des paiements ({totalPaiements:N2} TTC) ne peut pas dépasser le total de la facture ({ttc:N2} TTC).");
        }
    }

    public async Task AddPaiementAsync(int factureFournisseurId, PaiementFournisseur paiement, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var f = await db.FacturesFournisseurs
            .Include(x => x.Paiements)
            .FirstAsync(x => x.Id == factureFournisseurId, cancellationToken);

        var ttc = f.TotalTtc;
        var totalApres = f.Paiements.Sum(p => p.Montant) + paiement.Montant;
        EnsureTotalPaiementsNotOverTtc(ttc, totalApres);

        paiement.FactureFournisseurId = factureFournisseurId;
        db.PaiementsFournisseurs.Add(paiement);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePaiementAsync(int factureFournisseurId, int paiementId, decimal montant, DateTime date, ModePaiement mode, string reference, CancellationToken cancellationToken = default)
    {
        if (montant <= 0)
            throw new InvalidOperationException("Le montant doit être supérieur à 0.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var f = await db.FacturesFournisseurs
            .Include(x => x.Paiements)
            .FirstAsync(x => x.Id == factureFournisseurId, cancellationToken);

        var ttc = f.TotalTtc;
        var totalApres = f.Paiements.Where(x => x.Id != paiementId).Sum(x => x.Montant) + montant;
        EnsureTotalPaiementsNotOverTtc(ttc, totalApres);

        var p = await db.PaiementsFournisseurs.FirstAsync(x => x.Id == paiementId && x.FactureFournisseurId == factureFournisseurId, cancellationToken);
        p.Montant = montant;
        p.Date = date;
        p.Mode = mode;
        p.Reference = reference;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeletePaiementAsync(int factureFournisseurId, int paiementId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var p = await db.PaiementsFournisseurs.FirstAsync(x => x.Id == paiementId && x.FactureFournisseurId == factureFournisseurId, cancellationToken);
        db.PaiementsFournisseurs.Remove(p);
        await db.SaveChangesAsync(cancellationToken);
    }
}
