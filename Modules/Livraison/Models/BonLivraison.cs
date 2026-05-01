using GestionCommerciale.Shared.Models;

namespace GestionCommerciale.Modules.Livraison.Models;

public class BonLivraison : BaseEntity
{
    public string Numero { get; set; } = string.Empty;
    public int ClientId { get; set; }
    public int? DevisId { get; set; }
    public DateTime Date { get; set; }
    public StatutBL Statut { get; set; }
    public string Note { get; set; } = string.Empty;
    public List<BonLivraisonLigne> Lignes { get; set; } = [];
}
