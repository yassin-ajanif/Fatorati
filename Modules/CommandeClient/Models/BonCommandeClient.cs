using GestionCommerciale.Shared.Models;

namespace GestionCommerciale.Modules.CommandeClient.Models;

public class BonCommandeClient : BaseEntity
{
    public string Numero { get; set; } = string.Empty;
    public int ClientId { get; set; }
    public int? DevisId { get; set; }
    public DateTime Date { get; set; }
    public string Note { get; set; } = string.Empty;
    public List<BonCommandeClientLigne> Lignes { get; set; } = [];
}
