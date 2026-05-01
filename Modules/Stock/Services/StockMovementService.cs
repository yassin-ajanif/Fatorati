using GestionCommerciale.Modules.Stock.Models;
using GestionCommerciale.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace GestionCommerciale.Modules.Stock.Services;

public sealed class StockMovementService : IStockMovementService
{
    public async Task ApplyMovementAsync(
        AppDbContext db,
        int produitId,
        TypeMouvement type,
        decimal quantite,
        string origineType,
        int? origineId,
        string? note,
        int? createdByUserId,
        CancellationToken cancellationToken = default)
    {
        var produit = await db.Produits.FirstAsync(p => p.Id == produitId, cancellationToken);
        decimal delta = type switch
        {
            TypeMouvement.Entree => quantite,
            TypeMouvement.Sortie => -quantite,
            TypeMouvement.Ajustement => quantite,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        var stockAvant = produit.StockActuel;
        var next = stockAvant + delta;
        if (next < 0)
            throw new InvalidOperationException($"Stock insuffisant pour le produit {produit.Reference}.");

        produit.StockActuel = next;

        db.MouvementsStock.Add(new MouvementStock
        {
            ProduitId = produitId,
            Type = type,
            StockAvant = stockAvant,
            Quantite = quantite,
            OrigineType = origineType,
            OrigineId = origineId,
            Note = note ?? string.Empty,
            CreatedByUserId = createdByUserId
        });
    }
}
