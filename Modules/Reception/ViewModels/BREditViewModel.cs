using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionCommerciale.Modules.Auth.Services;
using GestionCommerciale.Modules.Stock;
using GestionCommerciale.Modules.Commande.Models;
using GestionCommerciale.Modules.Reception.Models;
using GestionCommerciale.Modules.Reception.Services;
using GestionCommerciale.Modules.Tiers.Models;
using GestionCommerciale.Shared.Database;
using GestionCommerciale.Shared.Helpers;
using GestionCommerciale.Shared.Services;
using GestionCommerciale.Shared.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestionCommerciale.Modules.Reception.ViewModels;

public partial class BREditViewModel : BaseViewModel
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IDocumentNumberService _numbers;
    private readonly IBonReceptionWorkflowService _workflow;
    private readonly IDialogService _dialog;
    private readonly WorkspaceNavigator _workspace;
    private readonly IServiceProvider _sp;
    private readonly ICurrentUserSession _session;
    private readonly ILocaleService _locale;
    private int? _sourceBonCommandeId;

    public BREditViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        IDocumentNumberService numbers,
        IBonReceptionWorkflowService workflow,
        IDialogService dialog,
        WorkspaceNavigator workspaceNavigator,
        IServiceProvider sp,
        ICurrentUserSession session,
        ILocaleService locale)
    {
        _dbFactory = dbFactory;
        _numbers = numbers;
        _workflow = workflow;
        _dialog = dialog;
        _workspace = workspaceNavigator;
        _sp = sp;
        _session = session;
        _locale = locale;
        _locale.CultureApplied += (_, _) => RefreshBrUi();
        Title = _locale.T("BR_Title");
        RefreshBrUi();
    }

    [ObservableProperty] private string _btnBack = string.Empty;
    [ObservableProperty] private string _btnSave = string.Empty;
    [ObservableProperty] private string _lblSupplier = string.Empty;
    [ObservableProperty] private string _wmSupplierSearch = string.Empty;
    [ObservableProperty] private string _lblDateBr = string.Empty;
    [ObservableProperty] private string _btnAddLine = string.Empty;
    [ObservableProperty] private string _btnApplyProduct = string.Empty;
    [ObservableProperty] private string _btnRemoveLine = string.Empty;
    [ObservableProperty] private string _lblAddProduct = string.Empty;
    [ObservableProperty] private string _wmAddProduct = string.Empty;
    [ObservableProperty] private string _statutLabel = string.Empty;
    [ObservableProperty] private string _addLineSearchText = string.Empty;
    [ObservableProperty] private object? _addLineCatalogPick;
    private bool _suppressAddLinePick;

    public AutoCompleteFilterPredicate<object?> ProduitAutocompleteFilter => ProductAutoComplete.ItemFilter;
    public AutoCompleteFilterPredicate<object?> PartyAutocompleteFilter => PartyAutoComplete.ItemFilter;

    private void RefreshBrUi()
    {
        BtnBack = _locale.T("Btn_Back");
        BtnSave = _locale.T("Btn_Save");
        LblSupplier = _locale.T("Lbl_Supplier");
        WmSupplierSearch = _locale.T("Wm_SearchSupplier");
        LblDateBr = _locale.T("Lbl_DateBR");
        BtnAddLine = _locale.T("Btn_AddLine");
        BtnApplyProduct = _locale.T("Btn_ApplyProduct");
        BtnRemoveLine = _locale.T("Btn_RemoveLine");
        LblAddProduct = _locale.T("Devis_LblAddProduct");
        WmAddProduct = _locale.T("Devis_WmSearchProduct");
        StatutLabel = UiEnumStrings.FormatStatutBR(_locale, Statut);
    }

    partial void OnStatutChanged(StatutBR value) =>
        StatutLabel = UiEnumStrings.FormatStatutBR(_locale, value);

    public ObservableCollection<GestionCommerciale.Modules.Tiers.Models.Tiers> Fournisseurs { get; } = [];
    public ObservableCollection<GestionCommerciale.Modules.Stock.Models.Produit> Produits { get; } = [];
    public ObservableCollection<BRLineRow> Lignes { get; } = [];

    [ObservableProperty] private int? _brId;
    [ObservableProperty] private int _fournisseurId;
    [ObservableProperty] private GestionCommerciale.Modules.Tiers.Models.Tiers? _selectedFournisseur;
    [ObservableProperty] private string _numero = string.Empty;
    [ObservableProperty] private DateTimeOffset _date = new(DateTime.Today);
    [ObservableProperty] private StatutBR _statut = StatutBR.Brouillon;
    [ObservableProperty] private string _note = string.Empty;
    [ObservableProperty] private bool _isReadOnly;
    [ObservableProperty] private BRLineRow? _selectedLine;

    public bool CanEdit => !IsReadOnly && Statut == StatutBR.Brouillon;

    partial void OnSelectedFournisseurChanged(GestionCommerciale.Modules.Tiers.Models.Tiers? value)
    {
        var id = value?.Id ?? 0;
        if (FournisseurId == id) return;
        FournisseurId = id;
    }

    partial void OnFournisseurIdChanged(int value)
    {
        if (SelectedFournisseur?.Id == value) return;
        SelectedFournisseur = Fournisseurs.FirstOrDefault(f => f.Id == value);
    }

    partial void OnAddLineCatalogPickChanged(object? value)
    {
        if (_suppressAddLinePick || !CanEdit) return;
        if (value is not GestionCommerciale.Modules.Stock.Models.Produit p) return;
        _suppressAddLinePick = true;
        var existing = Lignes.FirstOrDefault(l => l.ProduitId == p.Id && p.Id != 0);
        if (existing != null)
        {
            existing.QuantiteRecue += 1;
            SelectedLine = existing;
        }
        else
        {
            Lignes.Add(new BRLineRow
            {
                ProduitId = p.Id,
                Designation = p.Designation,
                QuantiteRecue = 1,
                PrixUnitaireHt = p.PrixAchatHT,
                TauxTva = p.TauxTVA
            });
            SelectedLine = Lignes.LastOrDefault();
        }
        AddLineCatalogPick = null;
        AddLineSearchText = string.Empty;
        _suppressAddLinePick = false;
    }

    public async Task LoadAsync(int? id, CancellationToken cancellationToken = default)
    {
        _sourceBonCommandeId = null;
        BrId = id;
        Lignes.Clear();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var fournisseurs = await db.Tiers.AsNoTracking()
            .Where(t => t.Actif && (t.Type == TypeTiers.Fournisseur || t.Type == TypeTiers.LesDeux))
            .OrderBy(t => t.Nom).ToListAsync(cancellationToken);
        Fournisseurs.Clear();
        foreach (var f in fournisseurs) Fournisseurs.Add(f);

        var produits = await db.Produits.AsNoTracking().Where(p => p.Actif)
            .SelectForListWithoutImageData().ToListAsync(cancellationToken);
        Produits.Clear();
        foreach (var p in produits) Produits.Add(p);

        if (id == null)
        {
            Numero = "(brouillon)";
            FournisseurId = Fournisseurs.FirstOrDefault()?.Id ?? 0;
            Statut = StatutBR.Brouillon;
            IsReadOnly = false;
            Title = _locale.T("BR_NewTitle");
            return;
        }

        var b = await db.BonsReception.Include(x => x.Lignes).FirstAsync(x => x.Id == id, cancellationToken);
        Numero = b.Numero;
        FournisseurId = b.FournisseurId;
        Date = new DateTimeOffset(b.Date);
        Statut = b.Statut;
        Note = b.Note;
        foreach (var l in b.Lignes)
        {
            Lignes.Add(new BRLineRow
            {
                ProduitId = l.ProduitId,
                Designation = l.Designation,
                QuantiteRecue = l.QuantiteRecue,
                PrixUnitaireHt = l.PrixUnitaireHT,
                TauxTva = l.TauxTVA
            });
        }

        IsReadOnly = Statut == StatutBR.Valide;
        Title = _locale.Tf("BR_TitleNum", Numero);
    }

    public void Load(int? id) => _ = LoadAsync(id, CancellationToken.None);

    /// <summary>Prépare un nouveau BR à partir d'un bon de commande validé (lignes et fournisseur copiés).</summary>
    public async Task<bool> LoadNewFromBonCommandeAsync(int bonCommandeId, CancellationToken cancellationToken = default)
    {
        BrId = null;
        _sourceBonCommandeId = bonCommandeId;
        Lignes.Clear();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var bc = await db.BonsCommande.Include(x => x.Lignes).FirstAsync(x => x.Id == bonCommandeId, cancellationToken);

        var fournisseurs = await db.Tiers.AsNoTracking()
            .Where(t => t.Actif && (t.Type == TypeTiers.Fournisseur || t.Type == TypeTiers.LesDeux))
            .OrderBy(t => t.Nom).ToListAsync(cancellationToken);
        Fournisseurs.Clear();
        foreach (var f in fournisseurs) Fournisseurs.Add(f);

        var produits = await db.Produits.AsNoTracking().Where(p => p.Actif)
            .SelectForListWithoutImageData().ToListAsync(cancellationToken);
        Produits.Clear();
        foreach (var p in produits) Produits.Add(p);

        FournisseurId = bc.FournisseurId;
        Date = new DateTimeOffset(DateTime.Today);
        Statut = StatutBR.Brouillon;
        Note = string.Empty;
        Numero = "(brouillon)";
        foreach (var l in bc.Lignes.OrderBy(x => x.Id))
        {
            Lignes.Add(new BRLineRow
            {
                ProduitId = l.ProduitId,
                Designation = l.Designation,
                QuantiteRecue = l.QuantiteCommandee,
                PrixUnitaireHt = l.PrixUnitaireHT,
                TauxTva = l.TauxTVA
            });
        }

        IsReadOnly = false;
        Title = _locale.Tf("BR_NewFromBc", bc.Numero);
        return true;
    }

    [RelayCommand]
    private void AddLine()
    {
        if (!CanEdit) return;
        var p = Produits.FirstOrDefault();
        Lignes.Add(new BRLineRow
        {
            ProduitId = p?.Id ?? 0,
            Designation = p?.Designation ?? string.Empty,
            QuantiteRecue = 1,
            PrixUnitaireHt = p?.PrixAchatHT ?? 0,
            TauxTva = p?.TauxTVA ?? 20
        });
    }

    [RelayCommand]
    private void RemoveLine(BRLineRow? row)
    {
        if (!CanEdit || row == null) return;
        Lignes.Remove(row);
    }

    [RelayCommand]
    private void ApplyProductToSelected() => ApplyProduct(SelectedLine);

    private void ApplyProduct(BRLineRow? row)
    {
        if (row == null) return;
        var p = Produits.FirstOrDefault(x => x.Id == row.ProduitId);
        if (p == null) return;
        row.Designation = p.Designation;
        row.PrixUnitaireHt = p.PrixAchatHT;
        row.TauxTva = p.TauxTVA;
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
            await _dialog.ShowErrorAsync(_locale.T("BR_DlgShort"), _locale.T("BR_ErrNoEdit"), cancellationToken);
            return;
        }

        if (FournisseurId == 0 || !Lignes.Any())
        {
            await _dialog.ShowErrorAsync(_locale.T("BR_DlgShort"), _locale.T("BR_ErrSupplierLines"), cancellationToken);
            return;
        }

        IsBusy = true;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            BonReception entity;
            if (BrId == null)
            {
                var num = await _numbers.NextBRAsync(cancellationToken);
                entity = new BonReception
                {
                    Numero = num,
                    BonCommandeId = _sourceBonCommandeId,
                    FournisseurId = FournisseurId,
                    Date = Date.DateTime,
                    Statut = StatutBR.Brouillon,
                    Note = Note,
                    CreatedByUserId = _session.UserId
                };
                foreach (var l in Lignes)
                {
                    entity.Lignes.Add(new BonReceptionLigne
                    {
                        ProduitId = l.ProduitId,
                        Designation = l.Designation,
                        QuantiteRecue = l.QuantiteRecue,
                        PrixUnitaireHT = l.PrixUnitaireHt,
                        TauxTVA = l.TauxTva
                    });
                }

                db.BonsReception.Add(entity);
                await db.SaveChangesAsync(cancellationToken);
                BrId = entity.Id;
                _sourceBonCommandeId = null;
            }
            else
            {
                entity = await db.BonsReception.Include(b => b.Lignes).FirstAsync(b => b.Id == BrId, cancellationToken);
                if (entity.Statut != StatutBR.Brouillon)
                {
                    await _dialog.ShowErrorAsync(_locale.T("BR_DlgShort"), _locale.T("BR_ErrDraftOnly"), cancellationToken);
                    return;
                }

                entity.FournisseurId = FournisseurId;
                entity.Date = Date.DateTime;
                entity.Note = Note;
                db.BonReceptionLignes.RemoveRange(entity.Lignes);
                foreach (var l in Lignes)
                {
                    entity.Lignes.Add(new BonReceptionLigne
                    {
                        ProduitId = l.ProduitId,
                        Designation = l.Designation,
                        QuantiteRecue = l.QuantiteRecue,
                        PrixUnitaireHT = l.PrixUnitaireHt,
                        TauxTVA = l.TauxTva
                    });
                }

                await db.SaveChangesAsync(cancellationToken);
            }

            try
            {
                await _workflow.ValiderAsync(entity.Id, _session.UserId, cancellationToken);
            }
            catch (Exception ex)
            {
                await _dialog.ShowErrorAsync(_locale.T("BR_DlgShort"), ex.Message, cancellationToken);
                await LoadAsync(BrId, cancellationToken);
                return;
            }

            Numero = entity.Numero;
            await _dialog.ShowInfoAsync(_locale.T("BR_DlgShort"), _locale.T("BR_Saved"), cancellationToken);
            await LoadAsync(BrId, cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Back()
    {
        var list = _sp.GetRequiredService<BRListViewModel>();
        _workspace.Open(list);
        list.LoadCommand.Execute(null);
    }
}
