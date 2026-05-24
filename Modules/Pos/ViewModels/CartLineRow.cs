using CommunityToolkit.Mvvm.ComponentModel;

namespace GestionCommerciale.Modules.Pos.ViewModels;

public partial class CartLineRow : ObservableObject
{
    public int ProduitId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public decimal PrixUnitaireHt { get; set; }
    public decimal TauxTva { get; set; }

    [ObservableProperty] private decimal _quantite = 1;

    public decimal PrixUnitaireTtc => PrixUnitaireHt * (1 + TauxTva / 100m);
    public decimal MontantHt => Quantite * PrixUnitaireHt;
    public decimal MontantTtc => Quantite * PrixUnitaireTtc;

    partial void OnQuantiteChanged(decimal value)
    {
        OnPropertyChanged(nameof(MontantHt));
        OnPropertyChanged(nameof(MontantTtc));
    }
}
