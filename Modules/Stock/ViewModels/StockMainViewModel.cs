using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionCommerciale.Modules.Auth.Services;
using GestionCommerciale.Modules.Stock;
using GestionCommerciale.Modules.Stock.Models;
using GestionCommerciale.Modules.Stock.Services;
using GestionCommerciale.Shared.Database;
using GestionCommerciale.Shared.Helpers;
using GestionCommerciale.Shared.Services;
using GestionCommerciale.Shared.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace GestionCommerciale.Modules.Stock.ViewModels;

public partial class StockMainViewModel : BaseViewModel
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IStockMovementService _stock;
    private readonly IDialogService _dialog;
    private readonly ICurrentUserSession _session;
    private readonly ILocaleService _locale;

    private int _currentProduitId;

    public StockMainViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        IStockMovementService stock,
        IDialogService dialog,
        ICurrentUserSession session,
        ILocaleService locale)
    {
        _dbFactory = dbFactory;
        _stock = stock;
        _dialog = dialog;
        _session = session;
        _locale = locale;
        _locale.CultureApplied += (_, _) => RefreshStockUi();
        RefreshStockUi();
        Pagination = new PaginationHelper(() => _ = LoadProduitsAsync(CancellationToken.None));
        MouvementPagination = new PaginationHelper(() => _ = LoadMouvementsAsync(_currentProduitId, CancellationToken.None));
    }

    public PaginationHelper Pagination { get; }
    public PaginationHelper MouvementPagination { get; }

    [ObservableProperty] private string _btnRefreshList = string.Empty;
    [ObservableProperty] private string _lblCatalog = string.Empty;
    [ObservableProperty] private string _helpStock = string.Empty;
    [ObservableProperty] private string _wmSearch = string.Empty;
    [ObservableProperty] private string _colRef = string.Empty;
    [ObservableProperty] private string _colDesignation = string.Empty;
    [ObservableProperty] private string _colStock = string.Empty;
    [ObservableProperty] private string _colMinDot = string.Empty;
    [ObservableProperty] private string _lblAdjustHistory = string.Empty;
    [ObservableProperty] private string _lblAdjustManual = string.Empty;
    [ObservableProperty] private string _lblVariation = string.Empty;
    [ObservableProperty] private string _lblMotifTrace = string.Empty;
    [ObservableProperty] private string _wmAdjustNote = string.Empty;
    [ObservableProperty] private string _btnApply = string.Empty;
    [ObservableProperty] private string _lblMovements = string.Empty;
    [ObservableProperty] private string _colDate = string.Empty;
    [ObservableProperty] private string _colStockCurrent = string.Empty;
    [ObservableProperty] private string _colBeforeQty = string.Empty;
    [ObservableProperty] private string _colQty = string.Empty;
    [ObservableProperty] private string _colDetail = string.Empty;

    private void RefreshStockUi()
    {
        Title = _locale.T("Stock_Title");
        BtnRefreshList = _locale.T("Btn_RefreshList");
        LblCatalog = _locale.T("Lbl_Catalog");
        HelpStock = _locale.T("Lbl_StockMainHelp");
        WmSearch = _locale.T("Wm_SearchProducts");
        ColRef = _locale.T("Lbl_ColRef");
        ColDesignation = _locale.T("Lbl_ColDesignation");
        ColStock = _locale.T("Lbl_ColStock");
        ColMinDot = _locale.T("Lbl_ColMinDot");
        LblAdjustHistory = _locale.T("Lbl_AdjustHistory");
        LblAdjustManual = _locale.T("Lbl_AdjustDelta");
        LblVariation = _locale.T("Lbl_Variation");
        LblMotifTrace = _locale.T("Lbl_MotifTrace");
        WmAdjustNote = _locale.T("Wm_AdjustNote");
        BtnApply = _locale.T("Btn_Apply");
        LblMovements = _locale.T("Lbl_MovementsForProduct");
        ColDate = _locale.T("Lbl_ColDate");
        ColStockCurrent = _locale.T("Lbl_ColStockCurrent");
        ColBeforeQty = _locale.T("Lbl_ColBeforeQty");
        ColQty = _locale.T("Lbl_ColQty");
        ColDetail = _locale.T("Lbl_ColDetail");
        if (SelectedProduit != null)
            _ = LoadMouvementsAsync(SelectedProduit.Id, CancellationToken.None);
    }

    partial void OnProductSearchChanged(string value)
    {
        Pagination.CurrentPage = 1;
        _ = LoadProduitsAsync(CancellationToken.None);
    }

    public ObservableCollection<Produit> Produits { get; } = [];
    public ObservableCollection<MouvementStock> Mouvements { get; } = [];

    [ObservableProperty] private Produit? _selectedProduit;

    [ObservableProperty] private string _productSearch = string.Empty;

    [ObservableProperty] private decimal _ajustementDelta;
    [ObservableProperty] private string _ajustementNote = string.Empty;

    [RelayCommand]
    private async Task LoadProduitsAsync(CancellationToken cancellationToken)
    {
        var prevId = SelectedProduit?.Id;
        IsBusy = true;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var q = db.Produits.AsNoTracking()
                .WhereSearchMatches(ProductSearch)
                .SelectForListWithoutImageData();
            var total = await q.CountAsync(cancellationToken);
            var list = await q
                .OrderBy(p => p.Reference)
                .Skip(Pagination.Skip).Take(Pagination.PageSize)
                .ToListAsync(cancellationToken);
            Produits.Clear();
            foreach (var p in list) Produits.Add(p);
            Pagination.TotalCount = total;
            if (prevId.HasValue)
                SelectedProduit = Produits.FirstOrDefault(p => p.Id == prevId.Value);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedProduitChanged(Produit? value)
    {
        Mouvements.Clear();
        if (value == null) return;
        _currentProduitId = value.Id;
        MouvementPagination.CurrentPage = 1;
        _ = LoadMouvementsAsync(value.Id, CancellationToken.None);
    }

    private async Task LoadMouvementsAsync(int produitId, CancellationToken cancellationToken)
    {
        if (produitId == 0) return;
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var q = db.MouvementsStock.AsNoTracking()
            .Where(m => m.ProduitId == produitId);
        var total = await q.CountAsync(cancellationToken);
        var list = await q
            .OrderByDescending(m => m.CreatedAt)
            .Skip(MouvementPagination.Skip).Take(MouvementPagination.PageSize)
            .ToListAsync(cancellationToken);
        Mouvements.Clear();
        foreach (var m in list) Mouvements.Add(m);
        MouvementPagination.TotalCount = total;
    }

    [RelayCommand]
    private async Task AjustementAsync(CancellationToken cancellationToken)
    {
        if (SelectedProduit == null) return;
        if (AjustementDelta == 0)
        {
            await _dialog.ShowErrorAsync(_locale.T("Stock_Title"), _locale.T("Stock_ErrVariation"), cancellationToken);
            return;
        }

        var id = SelectedProduit.Id;
        var libInventaire = _locale.T("Stock_DefaultMotif");
        var motif = AjustementNote.Trim();
        var detailNote = string.IsNullOrEmpty(motif)
            ? libInventaire
            : $"{libInventaire} — {motif}";
        IsBusy = true;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            await using var trx = await db.Database.BeginTransactionAsync(cancellationToken);
            await _stock.ApplyMovementAsync(
                db,
                id,
                TypeMouvement.Ajustement,
                AjustementDelta,
                libInventaire,
                null,
                detailNote,
                _session.UserId,
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await trx.CommitAsync(cancellationToken);
            AjustementDelta = 0;
            AjustementNote = string.Empty;
            await LoadProduitsAsync(cancellationToken);
            if (SelectedProduit != null)
                await LoadMouvementsAsync(SelectedProduit.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            await _dialog.ShowErrorAsync(_locale.T("Stock_Title"), ex.Message, cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
