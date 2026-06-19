using CommunityToolkit.Mvvm.ComponentModel;
using GestionCommerciale.Modules.Stock.Models;

namespace GestionCommerciale.Modules.CommandeClient.ViewModels;

public partial class BCVLineRow : ObservableObject
{
    [ObservableProperty] private int _produitId;
    [ObservableProperty] private string _reference = string.Empty;
    [ObservableProperty] private string _designation = string.Empty;
    [ObservableProperty] private string _conditionnement = string.Empty;
    [ObservableProperty] private decimal _quantiteCommandee;
    [ObservableProperty] private decimal _prixUnitaireHt;
    [ObservableProperty] private decimal _tauxTva;

    public decimal MontantHt => QuantiteCommandee * PrixUnitaireHt;

    public decimal MontantTtc => MontantHt * (1 + TauxTva / 100m);

    partial void OnQuantiteCommandeeChanged(decimal value) => NotifyMontants();
    partial void OnPrixUnitaireHtChanged(decimal value) => NotifyMontants();
    partial void OnTauxTvaChanged(decimal value) => NotifyMontants();

    public void ApplyCatalogProduct(Produit p)
    {
        ProduitId = p.Id;
        Reference = p.Reference;
        Designation = p.Designation;
        Conditionnement = p.Unite;
        PrixUnitaireHt = p.PrixVenteHT;
        TauxTva = p.TauxTVA;
        NotifyMontants();
    }

    private void NotifyMontants()
    {
        OnPropertyChanged(nameof(MontantHt));
        OnPropertyChanged(nameof(MontantTtc));
    }
}
