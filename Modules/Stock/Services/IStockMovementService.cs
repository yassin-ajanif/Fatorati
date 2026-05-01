using GestionCommerciale.Modules.Stock.Models;
using GestionCommerciale.Shared.Database;

namespace GestionCommerciale.Modules.Stock.Services;

public interface IStockMovementService
{
    Task ApplyMovementAsync(
        AppDbContext db,
        int produitId,
        TypeMouvement type,
        decimal quantite,
        string origineType,
        int? origineId,
        string? note,
        int? createdByUserId,
        CancellationToken cancellationToken = default);

    Task ResyncBonLivraisonStockAsync(
        AppDbContext db,
        int bonLivraisonId,
        string noteDetail,
        IEnumerable<(int ProduitId, decimal QuantiteLivree)> lines,
        int? createdByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Removes mouvements stock liés au BR et annule leur effet sur les quantités produit.</summary>
    Task StripBonReceptionMovementsAsync(AppDbContext db, int bonReceptionId, CancellationToken cancellationToken = default);
}
