using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionCommerciale.Modules.Facturation.ViewModels;
using GestionCommerciale.Modules.Pos.Models;
using GestionCommerciale.Modules.Pos.Services;
using GestionCommerciale.Modules.Stock.Models;
using GestionCommerciale.Shared.Services;
using GestionCommerciale.Shared.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GestionCommerciale.Modules.Pos.ViewModels;

public partial class PosViewModel : BaseViewModel
{
    private readonly IPosService _posService;
    private readonly ILocaleService _locale;
    private readonly IDialogService _dialog;
    private readonly IAppSettingsService _settings;
    private readonly WorkspaceNavigator _workspace;

    public PosViewModel(
        IPosService posService,
        ILocaleService locale,
        IDialogService dialog,
        IAppSettingsService settings,
        WorkspaceNavigator workspaceNavigator)
    {
        _posService = posService;
        _locale = locale;
        _dialog = dialog;
        _settings = settings;
        _workspace = workspaceNavigator;
        Title = _locale.T("Nav_Pos");
        _locale.CultureApplied += (_, _) =>
        {
            Title = _locale.T("Nav_Pos");
            OnPropertyChanged(nameof(SearchWatermark));
            OnPropertyChanged(nameof(CartTitle));
            OnPropertyChanged(nameof(TotalLabel));
            OnPropertyChanged(nameof(BtnClearCart));
            OnPropertyChanged(nameof(BtnCheckout));
        };
    }

    public ObservableCollection<ProductSearchRow> SearchResults { get; } = [];
    public ObservableCollection<CartLineRow> Cart { get; } = [];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private ProductSearchRow? _selectedProduct;

    public bool HasItems => Cart.Count > 0;

    public decimal TotalHt => Cart.Sum(l => l.MontantHt);
    public decimal TotalTtc => Cart.Sum(l => l.MontantTtc);

    public string SearchWatermark => _locale.T("Wm_SearchProducts");
    public string CartTitle => _locale.T("Nav_Pos");
    public string TotalLabel => "Total TTC";
    public string BtnClearCart => "Vider";
    public string BtnCheckout => "Encaisser";

    [RelayCommand]
    private async Task SearchProducts()
    {
        var list = await _posService.SearchProductsAsync(SearchText);
        SearchResults.Clear();
        foreach (var p in list)
            SearchResults.Add(new ProductSearchRow(p));
    }

    [RelayCommand]
    private void AddProduct(ProductSearchRow? row)
    {
        if (row?.Product is not { } produit) return;

        var existing = Cart.FirstOrDefault(l => l.ProduitId == produit.Id);
        if (existing is not null)
        {
            existing.Quantite++;
            NotifyTotals();
            return;
        }

        Cart.Add(new CartLineRow
        {
            ProduitId = produit.Id,
            Reference = produit.Reference,
            Designation = produit.Designation,
            PrixUnitaireHt = produit.PrixVenteHT,
            TauxTva = produit.TauxTVA,
            Quantite = 1
        });
        NotifyTotals();
    }

    [RelayCommand]
    private void RemoveProduct(CartLineRow? line)
    {
        if (line is null) return;
        Cart.Remove(line);
        NotifyTotals();
    }

    [RelayCommand]
    private void IncreaseQty(CartLineRow? line)
    {
        if (line is null) return;
        line.Quantite++;
        NotifyTotals();
    }

    [RelayCommand]
    private void DecreaseQty(CartLineRow? line)
    {
        if (line is null) return;
        if (line.Quantite <= 1)
        {
            Cart.Remove(line);
        }
        else
        {
            line.Quantite--;
        }
        NotifyTotals();
    }

    [RelayCommand]
    private void ClearCart()
    {
        Cart.Clear();
        NotifyTotals();
    }

    [RelayCommand]
    private async Task Checkout()
    {
        if (!HasItems) return;

        var clientId = await _posService.GetDefaultClientIdAsync();

        var cartData = Cart.Select(l => new CartLineData
        {
            ProduitId = l.ProduitId,
            Designation = l.Designation,
            Quantite = l.Quantite,
            PrixUnitaireHt = l.PrixUnitaireHt,
            TauxTva = l.TauxTva
        }).ToList();

        var facture = await _posService.CheckoutAsync(clientId, cartData);

        Cart.Clear();
        NotifyTotals();

        await _dialog.ShowInfoAsync("POS", $"Facture #{facture.Id} créée avec succès.");

        var factureVm = App.Services.GetRequiredService<FactureEditViewModel>();
        factureVm.Load(facture.Id);
        _workspace.Open(factureVm);
    }

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(TotalHt));
        OnPropertyChanged(nameof(TotalTtc));
    }
}
