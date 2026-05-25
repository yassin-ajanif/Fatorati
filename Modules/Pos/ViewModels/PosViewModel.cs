using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionCommerciale.Modules.Facturation.Models;
using GestionCommerciale.Modules.Pos.Models;
using GestionCommerciale.Modules.Pos.Services;
using GestionCommerciale.Modules.Stock.Models;
using GestionCommerciale.Shared.Helpers;
using GestionCommerciale.Shared.Services;
using GestionCommerciale.Shared.ViewModels;
using TiersEntity = GestionCommerciale.Modules.Tiers.Models.Tiers;

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
            OnPropertyChanged(nameof(WmClientSearch));
        };
        _ = LoadClientsAsync();
    }

    public ObservableCollection<ProductSearchRow> SearchResults { get; } = [];
    public ObservableCollection<CartLineRow> Cart { get; } = [];
    public ObservableCollection<TiersEntity> Clients { get; } = [];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private ProductSearchRow? _selectedProduct;
    [ObservableProperty] private TiersEntity? _selectedClient;

    public AutoCompleteFilterPredicate<object?> PartyAutocompleteFilter => PartyAutoComplete.ItemFilter;
    public string WmClientSearch => _locale.T("Wm_SearchClient");
    public decimal TotalPaiements => PaymentSplits.Sum(p => p.Montant);

    public ObservableCollection<PaymentSplitRow> PaymentSplits { get; } = [];

    private async Task LoadClientsAsync()
    {
        var clients = await _posService.GetActiveClientsAsync();
        Clients.Clear();
        foreach (var c in clients)
            Clients.Add(c);
    }

    public bool HasItems => Cart.Count > 0;

    public decimal TotalHt => Cart.Sum(l => l.MontantHt);
    public decimal TotalTtc => Cart.Sum(l => l.MontantTtc);

    public string SearchWatermark => _locale.T("Wm_SearchProducts");
    public string CartTitle => _locale.T("Nav_Pos");
    public string TotalLabel => "Total TTC";
    public string BtnClearCart => "Vider";
    public string BtnCheckout => "Encaisser";
    public string BtnAddPaymentSplit => "Ajouter mode";
    public bool CanRemovePaymentSplit => PaymentSplits.Count > 1;
    public string LabelMontantRecu => "Montant reçu";
    public string LabelResteARendre => "Reste à rendre";

    [ObservableProperty] private decimal _montantRecu;

    public decimal ResteARendre => MontantRecu >= TotalTtc ? MontantRecu - TotalTtc : 0;

    private void SyncPaymentSplits()
    {
        if (PaymentSplits.Count == 0)
        {
            PaymentSplits.Add(new PaymentSplitRow { Mode = ModePaiement.Especes, Montant = TotalTtc });
        }
        else if (PaymentSplits.Count == 1)
        {
            PaymentSplits[0].Montant = TotalTtc;
        }

        var allocated = PaymentSplits.Sum(p => p.Montant);
        if (PaymentSplits.Count > 1 && allocated != TotalTtc)
        {
            var diff = TotalTtc - allocated;
            PaymentSplits[^1].Montant += diff;
        }

        OnPropertyChanged(nameof(TotalPaiements));
        OnPropertyChanged(nameof(CanRemovePaymentSplit));
    }

    [RelayCommand]
    private void AddPaymentSplit()
    {
        PaymentSplits.Add(new PaymentSplitRow { Mode = ModePaiement.TPE, Montant = 0 });
        SyncPaymentSplits();
    }

    [RelayCommand]
    private void RemovePaymentSplit(PaymentSplitRow? row)
    {
        if (row is null || PaymentSplits.Count <= 1) return;
        PaymentSplits.Remove(row);
        SyncPaymentSplits();
    }

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
        MontantRecu = 0;
        NotifyTotals();
    }

    [RelayCommand]
    private async Task Checkout()
    {
        if (!HasItems) return;

        var requiresNamedClient = PaymentSplits.Any(p => p.Montant > 0 && (p.Mode == ModePaiement.Credit || p.Mode == ModePaiement.Cheque));
        if (requiresNamedClient && SelectedClient is null)
        {
            await _dialog.ShowErrorAsync("POS", "Veuillez sélectionner un client pour les paiements par Crédit ou Chèque.");
            return;
        }

        var clientId = SelectedClient?.Id ?? await _posService.GetDefaultClientIdAsync();

        var cartData = Cart.Select(l => new CartLineData
        {
            ProduitId = l.ProduitId,
            Designation = l.Designation,
            Quantite = l.Quantite,
            PrixUnitaireHt = l.PrixUnitaireHt,
            TauxTva = l.TauxTva
        }).ToList();

        var totalPaiements = PaymentSplits.Sum(p => p.Montant);
        if (totalPaiements > TotalTtc)
        {
            await _dialog.ShowErrorAsync("POS", "Le total des paiements ne peut pas dépasser le montant total TTC.");
            return;
        }

        var payments = PaymentSplits.Where(p => p.Montant > 0).Select(p => (p.Mode, p.Montant)).ToList();
        var facture = await _posService.CheckoutAsync(clientId, cartData, payments);

        Cart.Clear();
        SelectedClient = null;
        PaymentSplits.Clear();
        MontantRecu = 0;
        NotifyTotals();

        await _dialog.ShowInfoAsync("POS", $"Facture #{facture.Id} créée avec succès.", autoCloseMs: 1000);
    }

    partial void OnMontantRecuChanged(decimal value)
    {
        OnPropertyChanged(nameof(ResteARendre));
    }

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(TotalHt));
        OnPropertyChanged(nameof(TotalTtc));
        OnPropertyChanged(nameof(ResteARendre));
        SyncPaymentSplits();
    }
}
