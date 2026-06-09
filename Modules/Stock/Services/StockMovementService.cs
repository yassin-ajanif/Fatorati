using GestionCommerciale.Modules.Stock.Models;
using GestionCommerciale.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace GestionCommerciale.Modules.Stock.Services;

public sealed class StockMovementService : IStockMovementService
{
    public const string OrigineTypeBonLivraison = "BL";
    public const string OrigineTypeBonReception = "BR";
    public const string OrigineTypeAvoir = "Avoir";

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
        produit.StockActuel = stockAvant + delta;

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

    public async Task ResyncBonLivraisonStockAsync(
        AppDbContext db,
        int bonLivraisonId,
        string noteDetail,
        IEnumerable<(int ProduitId, decimal QuantiteLivree)> lines,
        int? createdByUserId,
        CancellationToken cancellationToken = default)
    {
        var old = await db.MouvementsStock
            .Where(m => m.OrigineType == OrigineTypeBonLivraison && m.OrigineId == bonLivraisonId)
            .ToListAsync(cancellationToken);

        foreach (var m in old)
        {
            var produit = await db.Produits.FirstAsync(p => p.Id == m.ProduitId, cancellationToken);
            switch (m.Type)
            {
                case TypeMouvement.Sortie:
                    produit.StockActuel += m.Quantite;
                    break;
                case TypeMouvement.Entree:
                    produit.StockActuel -= m.Quantite;
                    break;
                case TypeMouvement.Ajustement:
                    produit.StockActuel -= m.Quantite;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(m.Type), m.Type, null);
            }

            db.MouvementsStock.Remove(m);
        }

        foreach (var (produitId, qte) in lines)
        {
            if (produitId <= 0 || qte <= 0) continue;
            await ApplyMovementAsync(
                db,
                produitId,
                TypeMouvement.Sortie,
                qte,
                OrigineTypeBonLivraison,
                bonLivraisonId,
                noteDetail,
                createdByUserId,
                cancellationToken);
        }
    }

    public async Task StripBonReceptionMovementsAsync(AppDbContext db, int bonReceptionId, CancellationToken cancellationToken = default)
    {
        var old = await db.MouvementsStock
            .Where(m => m.OrigineType == OrigineTypeBonReception && m.OrigineId == bonReceptionId)
            .ToListAsync(cancellationToken);

        foreach (var m in old)
        {
            var produit = await db.Produits.FirstAsync(p => p.Id == m.ProduitId, cancellationToken);
            switch (m.Type)
            {
                case TypeMouvement.Entree:
                    produit.StockActuel -= m.Quantite;
                    break;
                case TypeMouvement.Sortie:
                    produit.StockActuel += m.Quantite;
                    break;
                case TypeMouvement.Ajustement:
                    produit.StockActuel -= m.Quantite;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(m.Type), m.Type, null);
            }

            db.MouvementsStock.Remove(m);
        }
    }

    public async Task StripAvoirMovementsAsync(AppDbContext db, int avoirId, CancellationToken cancellationToken = default)
    {
        var old = await db.MouvementsStock
            .Where(m => m.OrigineType == OrigineTypeAvoir && m.OrigineId == avoirId)
            .ToListAsync(cancellationToken);

        foreach (var m in old)
        {
            var produit = await db.Produits.FirstAsync(p => p.Id == m.ProduitId, cancellationToken);
            switch (m.Type)
            {
                case TypeMouvement.Entree:
                    produit.StockActuel -= m.Quantite;
                    break;
                case TypeMouvement.Sortie:
                    produit.StockActuel += m.Quantite;
                    break;
                case TypeMouvement.Ajustement:
                    produit.StockActuel -= m.Quantite;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(m.Type), m.Type, null);
            }

            db.MouvementsStock.Remove(m);
        }
    }
}
