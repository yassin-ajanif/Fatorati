using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionCommerciale.Modules.Auth.Services;
using GestionCommerciale.Modules.Stock;
using GestionCommerciale.Modules.Facturation.ViewModels;
using GestionCommerciale.Modules.Livraison.Models;
using GestionCommerciale.Modules.Livraison.Services;
using GestionCommerciale.Modules.Tiers.Models;
using GestionCommerciale.Shared.Database;
using GestionCommerciale.Shared.Helpers;
using GestionCommerciale.Shared.Services;
using GestionCommerciale.Shared.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestionCommerciale.Modules.Livraison.ViewModels;

public partial class BLEditViewModel : BaseViewModel
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IDocumentNumberService _numbers;
    private readonly IBonLivraisonWorkflowService _workflow;
    private readonly IDialogService _dialog;
    private readonly WorkspaceNavigator _workspace;
    private readonly IServiceProvider _sp;
    private readonly ICurrentUserSession _session;
    private readonly ILocaleService _locale;
    private readonly IUiPreferencesService _uiPreferences;

    public BLEditViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        IDocumentNumberService numbers,
        IBonLivraisonWorkflowService workflow,
        IDialogService dialog,
        WorkspaceNavigator workspaceNavigator,
        IServiceProvider sp,
        ICurrentUserSession session,
        ILocaleService locale,
        IUiPreferencesService uiPreferences)
    {
        _dbFactory = dbFactory;
        _numbers = numbers;
        _workflow = workflow;
        _dialog = dialog;
        _workspace = workspaceNavigator;
        _sp = sp;
        _session = session;
        _locale = locale;
        _uiPreferences = uiPreferences;
        _locale.CultureApplied += (_, _) => RefreshBlUi();
        LineGridColumns.PropertyChanged += OnLineGridColumnsPropertyChanged;
        _uiPreferences.LoadDocumentLineColumns("bon_livraison", LineGridColumns);
        Title = _locale.T("BL_Title");
        Lignes.CollectionChanged += LignesOnCollectionChanged;
        RefreshBlUi();
    }

    [ObservableProperty] private string _btnBack = string.Empty;
    [ObservableProperty] private string _btnSave = string.Empty;
    [ObservableProperty] private string _btnValidateStock = string.Empty;
    [ObservableProperty] private string _btnMarkDelivered = string.Empty;
    [ObservableProperty] private string _btnToInvoice = string.Empty;
    [ObservableProperty] private string _lblClient = string.Empty;
    [ObservableProperty] private string _wmClientSearch = string.Empty;
    [ObservableProperty] private string _lblDateBl = string.Empty;
    [ObservableProperty] private string _btnAddLine = string.Empty;
    [ObservableProperty] private string _btnApplyProduct = string.Empty;
    [ObservableProperty] private string _btnRemoveLine = string.Empty;
    [ObservableProperty] private string _lblAddProduct = string.Empty;
    [ObservableProperty] private string _wmAddProduct = string.Empty;
    [ObservableProperty] private string _statutLabel = string.Empty;
    [ObservableProperty] private string _lblDocLineColumnsHint = string.Empty;
    [ObservableProperty] private string _lblDocColRef = string.Empty;
    [ObservableProperty] private string _lblDocColDesignation = string.Empty;
    [ObservableProperty] private string _lblDocColQte = string.Empty;
    [ObservableProperty] private string _lblDocColCond = string.Empty;
    [ObservableProperty] private string _wmDocLineUnite = string.Empty;
    [ObservableProperty] private string _lblDocColPuHt = string.Empty;
    [ObservableProperty] private string _lblDocColRemise = string.Empty;
    [ObservableProperty] private string _lblDocColTva = string.Empty;
    [ObservableProperty] private string _lblDocColMontantHt = string.Empty;
    [ObservableProperty] private string _lblDocColMontantTtc = string.Empty;
    [ObservableProperty] private string _lblTotals = string.Empty;

    [ObservableProperty] private decimal _totalHt;
    [ObservableProperty] private decimal _totalTva;
    [ObservableProperty] private decimal _totalTtc;
    [ObservableProperty] private string _totalHtLabel = "HT 0,00 MAD";
    [ObservableProperty] private string _totalTvaLabel = "TVA 0,00 MAD";
    [ObservableProperty] private string _totalTtcLabel = "TTC 0,00 MAD";
    [ObservableProperty] private string _devise = "MAD";
    [ObservableProperty] private string _addLineSearchText = string.Empty;
    [ObservableProperty] private object? _addLineCatalogPick;
    private bool _suppressAddLinePick;

    public DocumentLineGridColumnState LineGridColumns { get; } = new(supportsLineRemise: false);

    public AutoCompleteFilterPredicate<object?> ProduitAutocompleteFilter => ProductAutoComplete.ItemFilter;
    public AutoCompleteFilterPredicate<object?> PartyAutocompleteFilter => PartyAutoComplete.ItemFilter;

    public bool ShowTotalTva => LineGridColumns.ShowTva && LineGridColumns.ShowMontantTtc;
    public bool ShowTotalTtc => LineGridColumns.ShowMontantTtc && LineGridColumns.ShowTva;
    public bool HighlightHtTotal => !ShowTotalTtc;

    private void OnLineGridColumnsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentLineGridColumnState.ShowTva) or nameof(DocumentLineGridColumnState.ShowMontantTtc))
        {
            OnPropertyChanged(nameof(ShowTotalTva));
            OnPropertyChanged(nameof(ShowTotalTtc));
            OnPropertyChanged(nameof(HighlightHtTotal));
            RefreshTotals();
        }

        _uiPreferences.SaveDocumentLineColumns("bon_livraison", LineGridColumns);
    }

    private void LignesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (BLLineRow row in e.NewItems)
                row.PropertyChanged += LineOnPropertyChanged;
        if (e.OldItems != null)
            foreach (BLLineRow row in e.OldItems)
                row.PropertyChanged -= LineOnPropertyChanged;
        RefreshTotals();
    }

    private void LineOnPropertyChanged(object? sender, PropertyChangedEventArgs e) => RefreshTotals();

    private void RefreshBlUi()
    {
        BtnBack = _locale.T("Btn_Back");
        BtnSave = _locale.T("Btn_Save");
        BtnValidateStock = _locale.T("Btn_ValidateStock");
        BtnMarkDelivered = _locale.T("Btn_MarkDelivered");
        BtnToInvoice = _locale.T("Btn_ToInvoice");
        LblClient = _locale.T("Lbl_Client");
        WmClientSearch = _locale.T("Wm_SearchClient");
        LblDateBl = _locale.T("Lbl_DateBL");
        BtnAddLine = _locale.T("Btn_AddLine");
        BtnApplyProduct = _locale.T("Btn_ApplyProduct");
        BtnRemoveLine = _locale.T("Btn_RemoveLine");
        LblAddProduct = _locale.T("Devis_LblAddProduct");
        WmAddProduct = _locale.T("Devis_WmSearchProduct");
        StatutLabel = UiEnumStrings.FormatStatutBL(_locale, Statut);
        LblDocLineColumnsHint = _locale.T("DocLine_ColumnsHint");
        LblDocColRef = _locale.T("DocLine_ColRef");
        LblDocColDesignation = _locale.T("DocLine_ColDesignation");
        LblDocColQte = _locale.T("DocLine_ColQte");
        LblDocColCond = _locale.T("DocLine_ColCond");
        WmDocLineUnite = _locale.T("DocLine_WmUnite");
        LblDocColPuHt = _locale.T("DocLine_ColPuHt");
        LblDocColRemise = _locale.T("DocLine_ColRemise");
        LblDocColTva = _locale.T("DocLine_ColTva");
        LblDocColMontantHt = _locale.T("DocLine_ColMontantHt");
        LblDocColMontantTtc = _locale.T("DocLine_ColMontantTtc");
        LblTotals = _locale.T("Lbl_Totals");
        TotalHtLabel = _locale.Tf("Doc_FmtHt", TotalHt, Devise);
        TotalTvaLabel = _locale.Tf("Doc_FmtTva", TotalTva, Devise);
        TotalTtcLabel = _locale.Tf("Doc_FmtTtc", TotalTtc, Devise);
    }

    partial void OnStatutChanged(StatutBL value) =>
        StatutLabel = UiEnumStrings.FormatStatutBL(_locale, value);

    public ObservableCollection<GestionCommerciale.Modules.Tiers.Models.Tiers> Clients { get; } = [];
    public ObservableCollection<GestionCommerciale.Modules.Stock.Models.Produit> Produits { get; } = [];
    public ObservableCollection<BLLineRow> Lignes { get; } = [];

    [ObservableProperty] private int? _blId;
    [ObservableProperty] private int? _devisId;
    [ObservableProperty] private int _clientId;
    [ObservableProperty] private GestionCommerciale.Modules.Tiers.Models.Tiers? _selectedClient;
    [ObservableProperty] private string _numero = string.Empty;
    [ObservableProperty] private DateTimeOffset _date = new(DateTime.Today);
    [ObservableProperty] private StatutBL _statut = StatutBL.Brouillon;
    [ObservableProperty] private string _note = string.Empty;
    [ObservableProperty] private bool _isReadOnly;
    [ObservableProperty] private BLLineRow? _selectedLine;

    public bool CanEdit => !IsReadOnly;

    partial void OnSelectedClientChanged(GestionCommerciale.Modules.Tiers.Models.Tiers? value)
    {
        var id = value?.Id ?? 0;
        if (ClientId == id) return;
        ClientId = id;
    }

    partial void OnClientIdChanged(int value)
    {
        if (SelectedClient?.Id == value) return;
        SelectedClient = Clients.FirstOrDefault(c => c.Id == value);
    }

    partial void OnAddLineCatalogPickChanged(object? value)
    {
        if (_suppressAddLinePick || !CanEdit) return;
        if (value is not GestionCommerciale.Modules.Stock.Models.Produit p) return;
        _suppressAddLinePick = true;
        var existing = Lignes.FirstOrDefault(l => l.ProduitId == p.Id && p.Id != 0);
        if (existing != null)
        {
            existing.QuantiteLivree += 1;
            existing.QuantiteCommandee += 1;
            SelectedLine = existing;
        }
        else
        {
            var row = new BLLineRow();
            row.ApplyCatalogProduct(p);
            row.QuantiteCommandee = 1;
            row.QuantiteLivree = 1;
            Lignes.Add(row);
            SelectedLine = row;
        }

        AddLineCatalogPick = null;
        AddLineSearchText = string.Empty;
        _suppressAddLinePick = false;
        RefreshTotals();
    }

    public async Task LoadAsync(int? id, CancellationToken cancellationToken = default)
    {
        BlId = id;
        DevisId = null;
        Lignes.Clear();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var clients = await db.Tiers.AsNoTracking()
            .Where(t => t.Actif && (t.Type == TypeTiers.Client || t.Type == TypeTiers.LesDeux))
            .OrderBy(t => t.Nom).ToListAsync(cancellationToken);
        Clients.Clear();
        foreach (var c in clients) Clients.Add(c);

        var produits = await db.Produits.AsNoTracking().Where(p => p.Actif)
            .SelectForListWithoutImageData().ToListAsync(cancellationToken);
        Produits.Clear();
        foreach (var p in produits) Produits.Add(p);
        Devise = "MAD";

        if (id == null)
        {
            Numero = "(brouillon)";
            ClientId = Clients.FirstOrDefault()?.Id ?? 0;
            Statut = StatutBL.Brouillon;
            IsReadOnly = false;
            Title = _locale.T("BL_NewTitle");
            RefreshTotals();
            return;
        }

        var b = await db.BonsLivraison.Include(x => x.Lignes).FirstAsync(x => x.Id == id, cancellationToken);
        DevisId = b.DevisId;
        Numero = b.Numero;
        ClientId = b.ClientId;
        Date = new DateTimeOffset(b.Date);
        Statut = b.Statut;
        Note = b.Note;
        foreach (var l in b.Lignes)
        {
            var prod = Produits.FirstOrDefault(p => p.Id == l.ProduitId);
            Lignes.Add(new BLLineRow
            {
                ProduitId = l.ProduitId,
                Reference = prod?.Reference ?? string.Empty,
                Designation = l.Designation,
                Conditionnement = prod?.Unite ?? string.Empty,
                QuantiteCommandee = l.QuantiteCommandee,
                QuantiteLivree = l.QuantiteLivree,
                PrixUnitaireHt = l.PrixUnitaireHT,
                TauxTva = l.TauxTVA
            });
        }

        IsReadOnly = false;
        Title = _locale.Tf("BL_TitleNum", Numero);
        RefreshTotals();
    }

    public void Load(int? id) => _ = LoadAsync(id, CancellationToken.None);

    public async Task LoadFromDevisAsync(int devisId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var clients = await db.Tiers.AsNoTracking()
            .Where(t => t.Actif && (t.Type == TypeTiers.Client || t.Type == TypeTiers.LesDeux))
            .OrderBy(t => t.Nom).ToListAsync(cancellationToken);
        Clients.Clear();
        foreach (var c in clients) Clients.Add(c);

        var produits = await db.Produits.AsNoTracking().Where(p => p.Actif)
            .SelectForListWithoutImageData().ToListAsync(cancellationToken);
        Produits.Clear();
        foreach (var p in produits) Produits.Add(p);
        Devise = "MAD";

        var d = await db.Devis.Include(x => x.Lignes).FirstAsync(x => x.Id == devisId, cancellationToken);
        DevisId = d.Id;
        ClientId = d.ClientId;
        Date = new DateTimeOffset(DateTime.Today);
        Statut = StatutBL.Brouillon;
        BlId = null;
        Numero = "(brouillon)";
        Lignes.Clear();
        foreach (var l in d.Lignes)
        {
            var prod = Produits.FirstOrDefault(p => p.Id == l.ProduitId);
            Lignes.Add(new BLLineRow
            {
                ProduitId = l.ProduitId,
                Reference = prod?.Reference ?? string.Empty,
                Designation = l.Designation,
                Conditionnement = prod?.Unite ?? string.Empty,
                QuantiteCommandee = l.Quantite,
                QuantiteLivree = l.Quantite,
                PrixUnitaireHt = l.PrixUnitaireHT,
                TauxTva = l.TauxTVA
            });
        }

        IsReadOnly = false;
        Title = _locale.T("BL_FromDevis");
        RefreshTotals();
    }

    public void LoadFromDevis(int devisId) => _ = LoadFromDevisAsync(devisId, CancellationToken.None);

    [RelayCommand]
    private void AddLine()
    {
        if (!CanEdit) return;
        var p = Produits.FirstOrDefault();
        Lignes.Add(new BLLineRow
        {
            ProduitId = p?.Id ?? 0,
            Reference = p?.Reference ?? string.Empty,
            Designation = p?.Designation ?? string.Empty,
            Conditionnement = p?.Unite ?? string.Empty,
            QuantiteCommandee = 1,
            QuantiteLivree = 1,
            PrixUnitaireHt = p?.PrixVenteHT ?? 0,
            TauxTva = p?.TauxTVA ?? 20
        });
    }

    [RelayCommand]
    private void RemoveLine(BLLineRow? row)
    {
        if (!CanEdit || row == null) return;
        Lignes.Remove(row);
    }

    [RelayCommand]
    private void ApplyProductToSelected() => ApplyProduct(SelectedLine);

    private void ApplyProduct(BLLineRow? row)
    {
        if (row == null) return;
        var p = Produits.FirstOrDefault(x => x.Id == row.ProduitId);
        if (p == null) return;
        row.Reference = p.Reference;
        row.Designation = p.Designation;
        row.Conditionnement = p.Unite;
        row.PrixUnitaireHt = p.PrixVenteHT;
        row.TauxTva = p.TauxTVA;
        RefreshTotals();
    }

    private void RefreshTotals()
    {
        var includeTvaInTotals = ShowTotalTtc;
        var ht = Lignes.Sum(l => l.MontantHt);
        var tva = includeTvaInTotals
            ? Lignes.Sum(l => l.MontantHt * (l.TauxTva / 100m))
            : 0m;
        var ttc = ht + tva;
        TotalHt = ht;
        TotalTva = tva;
        TotalTtc = ttc;
        TotalHtLabel = _locale.Tf("Doc_FmtHt", ht, Devise);
        TotalTvaLabel = _locale.Tf("Doc_FmtTva", tva, Devise);
        TotalTtcLabel = _locale.Tf("Doc_FmtTtc", ttc, Devise);
    }

    [RelayCommand]
    private void RemoveSelectedLine()
    {
        if (SelectedLine == null) return;
        RemoveLine(SelectedLine);
        SelectedLine = null;
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!CanEdit)
        {
            await _dialog.ShowErrorAsync(_locale.T("BL_DlgShort"), _locale.T("BL_ErrNoEdit"), cancellationToken);
            return;
        }

        if (ClientId == 0 || !Lignes.Any())
        {
            await _dialog.ShowErrorAsync(_locale.T("BL_DlgShort"), _locale.T("BL_ErrClientLines"), cancellationToken);
            return;
        }

        IsBusy = true;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            BonLivraison entity;
            if (BlId == null)
            {
                var num = await _numbers.NextBLAsync(cancellationToken);
                entity = new BonLivraison
                {
                    Numero = num,
                    ClientId = ClientId,
                    DevisId = DevisId,
                    Date = Date.DateTime,
                    Statut = Statut,
                    Note = Note,
                    CreatedByUserId = _session.UserId
                };
                foreach (var l in Lignes)
                {
                    entity.Lignes.Add(new BonLivraisonLigne
                    {
                        ProduitId = l.ProduitId,
                        Designation = l.Designation,
                        QuantiteCommandee = l.QuantiteCommandee,
                        QuantiteLivree = l.QuantiteLivree,
                        PrixUnitaireHT = l.PrixUnitaireHt,
                        TauxTVA = l.TauxTva
                    });
                }

                db.BonsLivraison.Add(entity);
                await db.SaveChangesAsync(cancellationToken);
                BlId = entity.Id;
            }
            else
            {
                entity = await db.BonsLivraison.Include(b => b.Lignes).FirstAsync(b => b.Id == BlId, cancellationToken);
                entity.ClientId = ClientId;
                entity.DevisId = DevisId;
                entity.Date = Date.DateTime;
                entity.Note = Note;
                entity.Statut = Statut;
                db.BonLivraisonLignes.RemoveRange(entity.Lignes);
                foreach (var l in Lignes)
                {
                    entity.Lignes.Add(new BonLivraisonLigne
                    {
                        ProduitId = l.ProduitId,
                        Designation = l.Designation,
                        QuantiteCommandee = l.QuantiteCommandee,
                        QuantiteLivree = l.QuantiteLivree,
                        PrixUnitaireHT = l.PrixUnitaireHt,
                        TauxTVA = l.TauxTva
                    });
                }

                await db.SaveChangesAsync(cancellationToken);
            }

            Numero = entity.Numero;
            await _dialog.ShowInfoAsync(_locale.T("BL_DlgShort"), _locale.T("BL_Saved"), cancellationToken);
            await LoadAsync(BlId, cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ValiderAsync(CancellationToken cancellationToken)
    {
        if (BlId == null) return;
        try
        {
            IsBusy = true;
            await _workflow.ValiderAsync(BlId.Value, _session.UserId, cancellationToken);
            await _dialog.ShowInfoAsync(_locale.T("BL_DlgShort"), _locale.T("BL_Validated"), cancellationToken);
            await LoadAsync(BlId, cancellationToken);
        }
        catch (Exception ex)
        {
            await _dialog.ShowErrorAsync(_locale.T("BL_DlgShort"), ex.Message, cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LivrerAsync(CancellationToken cancellationToken)
    {
        if (BlId == null) return;
        try
        {
            IsBusy = true;
            await _workflow.MarquerLivreAsync(BlId.Value, cancellationToken);
            await LoadAsync(BlId, cancellationToken);
        }
        catch (Exception ex)
        {
            await _dialog.ShowErrorAsync(_locale.T("BL_DlgShort"), ex.Message, cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToFacture()
    {
        if (BlId == null) return;
        var vm = _sp.GetRequiredService<FactureEditViewModel>();
        vm.LoadFromBL(BlId.Value);
        _workspace.Open(vm);
    }

    [RelayCommand]
    private void Back()
    {
        var list = _sp.GetRequiredService<BLListViewModel>();
        _workspace.Open(list);
        list.LoadCommand.Execute(null);
    }
}
