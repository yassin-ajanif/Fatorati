using GestionCommerciale.Modules.Devis.Models;
using GestionCommerciale.Modules.Facturation.Models;
using GestionCommerciale.Modules.Livraison.Models;
using GestionCommerciale.Modules.Commande.Models;
using GestionCommerciale.Modules.Reception.Models;
using GestionCommerciale.Shared.Models.Pdf;

namespace GestionCommerciale.Shared.Services;

public interface IPdfService
{
    Task<byte[]> BuildDevisPdfAsync(Devis devis, DocumentPartyPdfInfo party, CancellationToken cancellationToken = default);
    Task<byte[]> BuildBonLivraisonPdfAsync(BonLivraison bl, DocumentPartyPdfInfo party, CancellationToken cancellationToken = default);
    Task<byte[]> BuildBonReceptionPdfAsync(BonReception br, DocumentPartyPdfInfo party, CancellationToken cancellationToken = default);
    Task<byte[]> BuildBonCommandePdfAsync(BonCommande bc, DocumentPartyPdfInfo party, CancellationToken cancellationToken = default);
    Task<byte[]> BuildFacturePdfAsync(Facture facture, DocumentPartyPdfInfo party, CancellationToken cancellationToken = default);
    Task<byte[]> BuildAvoirPdfAsync(Avoir avoir, DocumentPartyPdfInfo party, CancellationToken cancellationToken = default);
}
