using GestionCommerciale.Modules.Livraison.Models;

namespace GestionCommerciale.Modules.Livraison.Services;

public interface IBonLivraisonWorkflowService
{
    Task ValiderAsync(int bonLivraisonId, int? userId, CancellationToken cancellationToken = default);
    Task MarquerLivreAsync(int bonLivraisonId, CancellationToken cancellationToken = default);
}
