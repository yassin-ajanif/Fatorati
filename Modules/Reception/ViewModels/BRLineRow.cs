using CommunityToolkit.Mvvm.ComponentModel;

namespace GestionCommerciale.Modules.Reception.ViewModels;

public partial class BRLineRow : ObservableObject
{
    [ObservableProperty] private int _produitId;
    [ObservableProperty] private string _designation = string.Empty;
    [ObservableProperty] private decimal _quantiteRecue;
    [ObservableProperty] private decimal _prixUnitaireHt;
    [ObservableProperty] private decimal _tauxTva;
}
