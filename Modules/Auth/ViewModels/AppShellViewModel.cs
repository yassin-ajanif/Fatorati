using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionCommerciale.Modules.Auth.Services;
using GestionCommerciale.Modules.Devis.ViewModels;
using GestionCommerciale.Modules.Facturation.ViewModels;
using GestionCommerciale.Modules.Livraison.ViewModels;
using GestionCommerciale.Modules.Commande.ViewModels;
using GestionCommerciale.Modules.Reception.ViewModels;
using GestionCommerciale.Modules.Stock.ViewModels;
using GestionCommerciale.Modules.Tiers.Models;
using GestionCommerciale.Modules.Tiers.ViewModels;
using GestionCommerciale.Shared.Services;
using GestionCommerciale.Shared.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GestionCommerciale.Modules.Auth.ViewModels;

public partial class AppShellViewModel : BaseViewModel
{
    private readonly WorkspaceNavigator _workspace;
    private readonly IServiceProvider _sp;
    private readonly ICurrentUserSession _session;
    private readonly ILocaleService _locale;

    public AppShellViewModel(
        WorkspaceNavigator workspaceNavigator,
        IServiceProvider sp,
        ICurrentUserSession session,
        ILocaleService locale)
    {
        _workspace = workspaceNavigator;
        _sp = sp;
        _session = session;
        _locale = locale;
        UserLabel = session.Nom ?? string.Empty;
        _workspace.CurrentPageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(WorkspaceCurrentPage));
            UpdateActiveNav();
        };
        _locale.CultureApplied += (_, _) => RefreshShellLabels();
        RefreshShellLabels();
        _workspace.Open(_sp.GetRequiredService<HomeViewModel>());
        UpdateActiveNav();
    }

    public BaseViewModel? WorkspaceCurrentPage => _workspace.CurrentPage;

    [ObservableProperty] private string _userLabel = string.Empty;

    [ObservableProperty] private string _navHome = string.Empty;
    [ObservableProperty] private string _navVente = string.Empty;
    [ObservableProperty] private string _navAchat = string.Empty;
    [ObservableProperty] private string _navClients = string.Empty;
    [ObservableProperty] private string _navDevis = string.Empty;
    [ObservableProperty] private string _navBl = string.Empty;
    [ObservableProperty] private string _navFactures = string.Empty;
    [ObservableProperty] private string _navFournisseurs = string.Empty;
    [ObservableProperty] private string _navBc = string.Empty;
    [ObservableProperty] private string _navBr = string.Empty;
    [ObservableProperty] private string _navStockAdmin = string.Empty;
    [ObservableProperty] private string _navStock = string.Empty;
    [ObservableProperty] private string _navProduits = string.Empty;
    [ObservableProperty] private string _navSettings = string.Empty;

    [ObservableProperty] private bool _isNavHomeActive;
    [ObservableProperty] private bool _isNavClientsActive;
    [ObservableProperty] private bool _isNavFournisseursActive;
    [ObservableProperty] private bool _isNavDevisActive;
    [ObservableProperty] private bool _isNavBlActive;
    [ObservableProperty] private bool _isNavFacturesActive;
    [ObservableProperty] private bool _isNavBcActive;
    [ObservableProperty] private bool _isNavBrActive;
    [ObservableProperty] private bool _isNavStockActive;
    [ObservableProperty] private bool _isNavProduitsActive;
    [ObservableProperty] private bool _isNavSettingsActive;

    private void RefreshShellLabels()
    {
        NavHome = _locale.T("Nav_Home");
        NavVente = _locale.T("Nav_Vente");
        NavAchat = _locale.T("Nav_Achat");
        NavClients = _locale.T("Nav_Clients");
        NavDevis = _locale.T("Nav_Devis");
        NavBl = _locale.T("Nav_BL");
        NavFactures = _locale.T("Nav_Factures");
        NavFournisseurs = _locale.T("Nav_Fournisseurs");
        NavBc = _locale.T("Nav_BC");
        NavBr = _locale.T("Nav_BR");
        NavStockAdmin = _locale.T("Nav_StockAdmin");
        NavStock = _locale.T("Nav_Stock");
        NavProduits = _locale.T("Nav_Produits");
        NavSettings = _locale.T("Nav_Settings");
        Title = NavHome;
    }

    [ObservableProperty] private bool _venteNavExpanded = true;
    [ObservableProperty] private bool _achatNavExpanded = true;
    [ObservableProperty] private bool _footerNavExpanded = true;

    public string VenteNavArrow => VenteNavExpanded ? "\u25BC" : "\u25B6";
    public string AchatNavArrow => AchatNavExpanded ? "\u25BC" : "\u25B6";
    public string FooterNavArrow => FooterNavExpanded ? "\u25BC" : "\u25B6";

    partial void OnVenteNavExpandedChanged(bool value) => OnPropertyChanged(nameof(VenteNavArrow));
    partial void OnAchatNavExpandedChanged(bool value) => OnPropertyChanged(nameof(AchatNavArrow));
    partial void OnFooterNavExpandedChanged(bool value) => OnPropertyChanged(nameof(FooterNavArrow));

    [RelayCommand]
    private void ToggleVenteNav() => VenteNavExpanded = !VenteNavExpanded;

    [RelayCommand]
    private void ToggleAchatNav() => AchatNavExpanded = !AchatNavExpanded;

    [RelayCommand]
    private void ToggleFooterNav() => FooterNavExpanded = !FooterNavExpanded;

    public bool ShowNavClients => _session.CanAccessClients;
    public bool ShowNavFournisseurs => _session.CanAccessFournisseurs;
    public bool ShowNavStock => _session.CanAccessStock;
    public bool ShowNavProduits => _session.CanAccessStock;
    public bool ShowNavDevis => _session.CanAccessDevis;
    public bool ShowNavBL => _session.CanAccessBL;
    public bool ShowNavBR => _session.CanAccessBR;
    public bool ShowNavBC => _session.CanAccessBC;
    public bool ShowNavFactures => _session.CanAccessFacturation;
    public bool ShowNavSettings => _session.CanAccessSettings;

    [RelayCommand]
    private void GoHome() => _workspace.Open(_sp.GetRequiredService<HomeViewModel>());

    [RelayCommand]
    private void GoClients()
    {
        var vm = _sp.GetRequiredService<TiersListViewModel>();
        vm.Configure(TiersListScope.Clients);
        _workspace.Open(vm);
    }

    [RelayCommand]
    private void GoFournisseurs()
    {
        var vm = _sp.GetRequiredService<TiersListViewModel>();
        vm.Configure(TiersListScope.Fournisseurs);
        _workspace.Open(vm);
    }

    [RelayCommand]
    private void GoStock() => _workspace.Open(_sp.GetRequiredService<StockMainViewModel>());

    [RelayCommand]
    private void GoProduits() => _workspace.Open(_sp.GetRequiredService<ProduitsViewModel>());

    [RelayCommand]
    private void GoDevis() => _workspace.Open(_sp.GetRequiredService<DevisListViewModel>());

    [RelayCommand]
    private void GoBL() => _workspace.Open(_sp.GetRequiredService<BLListViewModel>());

    [RelayCommand]
    private void GoBR() => _workspace.Open(_sp.GetRequiredService<BRListViewModel>());

    [RelayCommand]
    private void GoBC()
    {
        var vm = _sp.GetRequiredService<BCListViewModel>();
        _workspace.Open(vm);
        vm.LoadCommand.Execute(null);
    }

    [RelayCommand]
    private void GoFactures() => _workspace.Open(_sp.GetRequiredService<FactureListViewModel>());

    [RelayCommand]
    private void GoSettings() => _workspace.Open(_sp.GetRequiredService<SettingsViewModel>());

    private void UpdateActiveNav()
    {
        var p = _workspace.CurrentPage;
        IsNavHomeActive = p is HomeViewModel;
        IsNavClientsActive = p is TiersListViewModel tl && tl.Scope == TiersListScope.Clients
            || p is TiersDetailViewModel td && td.ListScope == TiersListScope.Clients;
        IsNavFournisseursActive = p is TiersListViewModel tiersList && tiersList.Scope == TiersListScope.Fournisseurs
            || p is TiersDetailViewModel tiersDetail && tiersDetail.ListScope == TiersListScope.Fournisseurs;
        IsNavDevisActive = p is DevisListViewModel or DevisEditViewModel;
        IsNavBlActive = p is BLListViewModel or BLEditViewModel;
        IsNavFacturesActive = p is FactureListViewModel or FactureEditViewModel or AvoirEditViewModel;
        IsNavBcActive = p is BCListViewModel or BCEditViewModel;
        IsNavBrActive = p is BRListViewModel or BREditViewModel;
        IsNavStockActive = p is StockMainViewModel;
        IsNavProduitsActive = p is ProduitsViewModel;
        IsNavSettingsActive = p is SettingsViewModel;
    }
}
