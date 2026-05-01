using GestionCommerciale.Shared.Models;

namespace GestionCommerciale.Modules.Stock.Models;

public class MouvementStock : BaseEntity
{
    public int ProduitId { get; set; }
    public Produit? Produit { get; set; }
    public TypeMouvement Type { get; set; }
    public decimal StockAvant { get; set; }
    public decimal Quantite { get; set; }
    public string OrigineType { get; set; } = string.Empty;
    public int? OrigineId { get; set; }
    public string Note { get; set; } = string.Empty;
}
