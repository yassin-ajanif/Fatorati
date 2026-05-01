using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionCommerciale.Modules.Livraison.Models;
using GestionCommerciale.Modules.Stock.Services;
using GestionCommerciale.Shared.Helpers;
using GestionCommerciale.Shared.Database;
using GestionCommerciale.Shared.Models.Pdf;
using GestionCommerciale.Shared.Services;
using GestionCommerciale.Shared.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestionCommerciale.Modules.Livraison.ViewModels;

public partial class BLListViewModel : BaseViewModel
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly WorkspaceNavigator _workspace;
    private readonly IServiceProvider _sp;
    private readonly IDialogService _dialog;
    private readonly IPdfService _pdf;
    private readonly ILocaleService _locale;
    private readonly IStockMovementService _stock;
    private readonly IAppSettingsService _settings;

    public BLListViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        WorkspaceNavigator workspaceNavigator,
        IServiceProvider sp,
        IDialogService dialog,
        IPdfService pdf,
        ILocaleService locale,
        IStockMovementService stock,
        IAppSettingsService settings)
    {
        _dbFactory = dbFactory;
        _workspace = workspaceNavigator;
        _sp = sp;
        _dialog = dialog;
        _pdf = pdf;
        _locale = locale;
        _stock = stock;
        _settings = settings;
        _locale.CultureApplied += (_, _) => RefreshListToolbar();
        RefreshListToolbar();
        Title = _locale.T("BLList_Title");
    }

    [ObservableProperty] private string _btnRefresh = string.Empty;
    [ObservableProperty] private string _btnNew = string.Empty;
    [ObservableProperty] private string _btnPdf = string.Empty;
    [ObservableProperty] private string _menuDeleteBl = string.Empty;
    [ObservableProperty] private string _colHeaderRef = string.Empty;
    [ObservableProperty] private string _colHeaderParty = string.Empty;
    [ObservableProperty] private string _colHeaderDate = string.Empty;
    [ObservableProperty] private string _colHeaderHt = string.Empty;
    [ObservableProperty] private string _colHeaderTtc = string.Empty;
    [ObservableProperty] private string _colHeaderNote = string.Empty;
    [ObservableProperty] private string _searchWatermark = string.Empty;

    private readonly List<BLListRow> _allRows = [];

    private void RefreshListToolbar()
    {
        BtnRefresh = _locale.T("Btn_Refresh");
        BtnNew = _locale.T("Btn_New");
        BtnPdf = _locale.T("Btn_Pdf");
        MenuDeleteBl = _locale.T("BL_MenuDelete");
        ColHeaderRef = _locale.T("DevisList_ColRef");
        ColHeaderParty = _locale.T("Lbl_Client");
        ColHeaderDate = _locale.T("DevisList_ColDate");
        ColHeaderHt = _locale.T("DevisList_ColHt");
        ColHeaderTtc = _locale.T("DevisList_ColTtc");
        ColHeaderNote = _locale.T("DevisList_ColNote");
        SearchWatermark = _locale.T("DocList_SearchPlaceholderClient");
    }

    public ObservableCollection<BLListRow> Items { get; } = [];
    [ObservableProperty] private BLListRow? _selected;
    [ObservableProperty] private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value) => ApplySearchFilter();

    private void ApplySearchFilter()
    {
        var selId = Selected?.Bl.Id;
        Items.Clear();
        foreach (var r in _allRows)
        {
            if (DocumentListFilter.Matches(SearchText, r.Bl.Numero, r.ClientNom))
                Items.Add(r);
        }
        if (selId is { } id)
            Selected = Items.FirstOrDefault(x => x.Bl.Id == id);
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var cfg = await _settings.GetAsync(cancellationToken);
            var devise = string.IsNullOrWhiteSpace(cfg.Devise) ? "MAD" : cfg.Devise.Trim();
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var list = await db.BonsLivraison.AsNoTracking().Include(b => b.Lignes).OrderByDescending(b => b.Date).Take(200).ToListAsync(cancellationToken);
            var ids = list.Select(b => b.ClientId).Distinct().ToList();
            var noms = await db.Tiers.AsNoTracking()
                .Where(t => ids.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Nom, cancellationToken);
            _allRows.Clear();
            foreach (var b in list)
                _allRows.Add(BLListRow.Create(b, noms.GetValueOrDefault(b.ClientId) ?? string.Empty, devise, _locale));
            ApplySearchFilter();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewBl()
    {
        var vm = _sp.GetRequiredService<BLEditViewModel>();
        vm.Load(null);
        _workspace.Open(vm);
    }

    [RelayCommand]
    private void OpenSelected()
    {
        if (Selected == null) return;
        var vm = _sp.GetRequiredService<BLEditViewModel>();
        vm.Load(Selected.Bl.Id);
        _workspace.Open(vm);
    }

    [RelayCommand]
    private async Task DeleteBlAsync(BLListRow? row, CancellationToken cancellationToken)
    {
        if (row == null) return;
        var item = row.Bl;

        if (!await _dialog.ConfirmAsync(_locale.T("BL_DlgShort"), _locale.Tf("BL_ConfirmDelete", item.Numero), cancellationToken))
            return;

        IsBusy = true;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            if (await db.Factures.AsNoTracking().AnyAsync(f => f.BLId == item.Id, cancellationToken))
            {
                await _dialog.ShowErrorAsync(_locale.T("BL_DlgShort"), _locale.T("BL_ErrDeleteReferenced"), cancellationToken);
                return;
            }

            var entity = await db.BonsLivraison.Include(b => b.Lignes).FirstAsync(b => b.Id == item.Id, cancellationToken);
            await _stock.ResyncBonLivraisonStockAsync(db, entity.Id, entity.Numero, Enumerable.Empty<(int ProduitId, decimal QuantiteLivree)>(), null, cancellationToken);
            db.BonsLivraison.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
            if (Selected?.Bl.Id == item.Id)
                Selected = null;
            Items.Remove(row);
            _allRows.Remove(row);
            await _dialog.ShowInfoAsync(_locale.T("BL_DlgShort"), _locale.T("BL_Deleted"), cancellationToken);
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
    private async Task ExportPdfAsync(CancellationToken cancellationToken)
    {
        if (Selected == null) return;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var b = await db.BonsLivraison.Include(x => x.Lignes).FirstAsync(x => x.Id == Selected.Bl.Id, cancellationToken);
            var client = await db.Tiers.AsNoTracking().FirstAsync(t => t.Id == b.ClientId, cancellationToken);
            var bytes = await _pdf.BuildBonLivraisonPdfAsync(b, DocumentPartyPdfInfo.FromTiers(client), cancellationToken);
            var ok = await _dialog.SavePickedFileBytesAsync(_locale.T("Export_PdfPicker"), $"{b.Numero}.pdf", new[] { "*.pdf" }, bytes, cancellationToken);
            if (ok)
                await _dialog.ShowInfoAsync(_locale.T("Export_Pdf"), _locale.T("Export_Done"), cancellationToken);
        }
        catch (Exception ex)
        {
            await _dialog.ShowErrorAsync(_locale.T("Export_Pdf"), ex.Message, cancellationToken);
        }
    }
}
