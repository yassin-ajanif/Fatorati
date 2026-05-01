using System.ComponentModel.DataAnnotations.Schema;
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

    [NotMapped]
    public decimal StockApres => Type switch
    {
        TypeMouvement.Entree => StockAvant + Quantite,
        TypeMouvement.Sortie => StockAvant - Quantite,
        TypeMouvement.Ajustement => StockAvant + Quantite,
        _ => StockAvant
    };

    [NotMapped]
    public string TraceDetail => string.IsNullOrWhiteSpace(Note) ? OrigineType : Note;
}
