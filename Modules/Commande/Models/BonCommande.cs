using GestionCommerciale.Shared.Models;

namespace GestionCommerciale.Modules.Commande.Models;

public class BonCommande : BaseEntity
{
    public string Numero { get; set; } = string.Empty;
    public int FournisseurId { get; set; }
    public DateTime Date { get; set; }
    public string Note { get; set; } = string.Empty;
    public List<BonCommandeLigne> Lignes { get; set; } = [];
}
