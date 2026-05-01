using GestionCommerciale.Modules.Livraison.Models;
using GestionCommerciale.Modules.Stock.Models;
using GestionCommerciale.Modules.Stock.Services;
using GestionCommerciale.Shared.Database;
using GestionCommerciale.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace GestionCommerciale.Modules.Livraison.Services;

public sealed class BonLivraisonWorkflowService : IBonLivraisonWorkflowService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IStockMovementService _stock;
    private readonly IAppSettingsService _settings;

    public BonLivraisonWorkflowService(
        IDbContextFactory<AppDbContext> dbFactory,
        IStockMovementService stock,
        IAppSettingsService settings)
    {
        _dbFactory = dbFactory;
        _stock = stock;
        _settings = settings;
    }

    public async Task ValiderAsync(int bonLivraisonId, int? userId, CancellationToken cancellationToken = default)
    {
        var cfg = await _settings.GetAsync(cancellationToken);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var trx = await db.Database.BeginTransactionAsync(cancellationToken);

        var bl = await db.BonsLivraison
            .Include(b => b.Lignes)
            .FirstAsync(b => b.Id == bonLivraisonId, cancellationToken);

        if (bl.Statut != StatutBL.Brouillon)
            throw new InvalidOperationException("Seuls les brouillons peuvent être validés.");

        foreach (var ligne in bl.Lignes)
        {
            if (ligne.QuantiteLivree > ligne.QuantiteCommandee)
                throw new InvalidOperationException("Quantité livrée supérieure à la quantité commandée.");

            if (ligne.QuantiteLivree <= 0) continue;

            if (cfg.BlocageSiStockInsuffisant)
            {
                var p = await db.Produits.AsNoTracking().FirstAsync(x => x.Id == ligne.ProduitId, cancellationToken);
                if (p.StockActuel < ligne.QuantiteLivree)
                    throw new InvalidOperationException($"Stock insuffisant pour {p.Reference}.");
            }

            await _stock.ApplyMovementAsync(
                db,
                ligne.ProduitId,
                TypeMouvement.Sortie,
                ligne.QuantiteLivree,
                "BL",
                bl.Id,
                $"BL {bl.Numero}",
                userId,
                cancellationToken);
        }

        bl.Statut = StatutBL.Valide;
        await db.SaveChangesAsync(cancellationToken);
        await trx.CommitAsync(cancellationToken);
    }

    public async Task MarquerLivreAsync(int bonLivraisonId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var bl = await db.BonsLivraison.FirstAsync(b => b.Id == bonLivraisonId, cancellationToken);
        if (bl.Statut != StatutBL.Valide)
            throw new InvalidOperationException("Le BL doit être validé avant livraison.");
        bl.Statut = StatutBL.Livre;
        await db.SaveChangesAsync(cancellationToken);
    }
}
