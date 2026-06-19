using GestionCommerciale.Modules.Facturation.Models;
using GestionCommerciale.Modules.Stock.Models;
using GestionCommerciale.Modules.Stock.Services;
using GestionCommerciale.Shared.Database;
using GestionCommerciale.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace GestionCommerciale.Modules.Facturation.Services;

public sealed class AvoirWorkflowService : IAvoirWorkflowService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IStockMovementService _stock;

    public AvoirWorkflowService(IDbContextFactory<AppDbContext> dbFactory, IStockMovementService stock)
    {
        _dbFactory = dbFactory;
        _stock = stock;
    }

    public async Task CreerEtValiderAsync(int avoirId, int? userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var trx = await db.Database.BeginTransactionAsync(cancellationToken);

        var avoir = await db.Avoirs
            .Include(a => a.Lignes)
            .FirstAsync(a => a.Id == avoirId, cancellationToken);

        if (avoir.FactureId.HasValue)
        {
            var facture = await db.Factures
                .Include(f => f.Paiements)
                .FirstAsync(f => f.Id == avoir.FactureId.Value, cancellationToken);

            var ttcFacture = facture.TotalTtc;
            var (_, _, ttcAvoir) = DocumentTotalsHelper.AvoirTotals(avoir.Lignes);

            var existingAvoirs = await db.Avoirs
                .Where(a => a.FactureId == facture.Id && a.Id != avoir.Id)
                .Include(a => a.Lignes)
                .ToListAsync(cancellationToken);
            decimal deja = 0;
            foreach (var a in existingAvoirs)
                deja += DocumentTotalsHelper.AvoirTotals(a.Lignes).ttc;

            if (deja + ttcAvoir > ttcFacture + 0.01m)
                throw new InvalidOperationException("Montant avoir supérieur au reste disponible sur la facture.");
        }

        if (avoir.RetourMarchandise)
        {
            foreach (var ligne in avoir.Lignes)
            {
                if (ligne.Quantite <= 0) continue;
                await _stock.ApplyMovementAsync(
                    db,
                    ligne.ProduitId,
                    TypeMouvement.Entree,
                    ligne.Quantite,
                    "Avoir",
                    avoir.Id,
                    $"Avoir {avoir.Numero}",
                    userId,
                    cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await trx.CommitAsync(cancellationToken);
    }
}
