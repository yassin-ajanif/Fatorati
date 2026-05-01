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
}
