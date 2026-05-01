using GestionCommerciale.Modules.Reception.Models;
using GestionCommerciale.Modules.Stock.Models;
using GestionCommerciale.Modules.Stock.Services;
using GestionCommerciale.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace GestionCommerciale.Modules.Reception.Services;

public sealed class BonReceptionWorkflowService : IBonReceptionWorkflowService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IStockMovementService _stock;

    public BonReceptionWorkflowService(IDbContextFactory<AppDbContext> dbFactory, IStockMovementService stock)
    {
        _dbFactory = dbFactory;
        _stock = stock;
    }

    public async Task ValiderAsync(int bonReceptionId, int? userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var trx = await db.Database.BeginTransactionAsync(cancellationToken);

        var br = await db.BonsReception
            .Include(b => b.Lignes)
            .FirstAsync(b => b.Id == bonReceptionId, cancellationToken);

        await ReplayBonReceptionLinesIntoStockAsync(db, br, userId, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await trx.CommitAsync(cancellationToken);
    }

    public async Task ResyncStockFromLinesAsync(int bonReceptionId, int? userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var trx = await db.Database.BeginTransactionAsync(cancellationToken);

        var br = await db.BonsReception
            .Include(b => b.Lignes)
            .FirstAsync(b => b.Id == bonReceptionId, cancellationToken);

        await ReplayBonReceptionLinesIntoStockAsync(db, br, userId, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await trx.CommitAsync(cancellationToken);
    }

    private async Task ReplayBonReceptionLinesIntoStockAsync(
        AppDbContext db,
        BonReception br,
        int? userId,
        CancellationToken cancellationToken)
    {
        await _stock.StripBonReceptionMovementsAsync(db, br.Id, cancellationToken);

        foreach (var ligne in br.Lignes.OrderBy(l => l.Id))
        {
            if (ligne.QuantiteRecue <= 0) continue;

            var produit = await db.Produits.FirstAsync(p => p.Id == ligne.ProduitId, cancellationToken);
            var oldQty = produit.StockActuel;
            var oldPrice = produit.PrixAchatHT;
            var newQty = ligne.QuantiteRecue;
            var newPrice = ligne.PrixUnitaireHT;

            var totalQty = oldQty + newQty;
            if (totalQty > 0)
                produit.PrixAchatHT = (oldQty * oldPrice + newQty * newPrice) / totalQty;

            await _stock.ApplyMovementAsync(
                db,
                ligne.ProduitId,
                TypeMouvement.Entree,
                ligne.QuantiteRecue,
                StockMovementService.OrigineTypeBonReception,
                br.Id,
                br.Numero,
                userId,
                cancellationToken);
        }
    }
}
