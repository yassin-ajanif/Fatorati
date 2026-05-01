using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionCommerciale.Modules.Reception.Models;
using GestionCommerciale.Modules.Stock.Services;
using GestionCommerciale.Shared.Helpers;
using GestionCommerciale.Shared.Database;
using GestionCommerciale.Shared.Models.Pdf;
using GestionCommerciale.Shared.Services;
using GestionCommerciale.Shared.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestionCommerciale.Modules.Reception.ViewModels;

public partial class BRListViewModel : BaseViewModel
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly WorkspaceNavigator _workspace;
    private readonly IServiceProvider _sp;
    private readonly IDialogService _dialog;
    private readonly IPdfService _pdf;
    private readonly ILocaleService _locale;
    private readonly IStockMovementService _stock;
    private readonly IAppSettingsService _settings;

    public BRListViewModel(
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
        Title = _locale.T("BRList_Title");
    }

    [ObservableProperty] private string _btnRefresh = string.Empty;
    [ObservableProperty] private string _btnNew = string.Empty;
    [ObservableProperty] private string _btnPdf = string.Empty;
    [ObservableProperty] private string _menuDeleteBr = string.Empty;
    [ObservableProperty] private string _colHeaderRef = string.Empty;
    [ObservableProperty] private string _colHeaderParty = string.Empty;
    [ObservableProperty] private string _colHeaderDate = string.Empty;
    [ObservableProperty] private string _colHeaderHt = string.Empty;
    [ObservableProperty] private string _colHeaderTtc = string.Empty;
    [ObservableProperty] private string _colHeaderNote = string.Empty;
    [ObservableProperty] private string _searchWatermark = string.Empty;

    private readonly List<BRListRow> _allRows = [];

    private void RefreshListToolbar()
    {
        BtnRefresh = _locale.T("Btn_Refresh");
        BtnNew = _locale.T("Btn_New");
        BtnPdf = _locale.T("Btn_Pdf");
        MenuDeleteBr = _locale.T("BR_MenuDelete");
        ColHeaderRef = _locale.T("DevisList_ColRef");
        ColHeaderParty = _locale.T("Lbl_Supplier");
        ColHeaderDate = _locale.T("DevisList_ColDate");
        ColHeaderHt = _locale.T("DevisList_ColHt");
        ColHeaderTtc = _locale.T("DevisList_ColTtc");
        ColHeaderNote = _locale.T("DevisList_ColNote");
        SearchWatermark = _locale.T("DocList_SearchPlaceholderSupplier");
    }

    public ObservableCollection<BRListRow> Items { get; } = [];
    [ObservableProperty] private BRListRow? _selected;
    [ObservableProperty] private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value) => ApplySearchFilter();

    private void ApplySearchFilter()
    {
        var selId = Selected?.Br.Id;
        Items.Clear();
        foreach (var r in _allRows)
        {
            if (DocumentListFilter.Matches(SearchText, r.Br.Numero, r.FournisseurNom))
                Items.Add(r);
        }
        if (selId is { } id)
            Selected = Items.FirstOrDefault(x => x.Br.Id == id);
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
            var list = await db.BonsReception.AsNoTracking().Include(b => b.Lignes).OrderByDescending(b => b.Date).Take(200).ToListAsync(cancellationToken);
            var ids = list.Select(b => b.FournisseurId).Distinct().ToList();
            var noms = await db.Tiers.AsNoTracking()
                .Where(t => ids.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Nom, cancellationToken);
            _allRows.Clear();
            foreach (var b in list)
                _allRows.Add(BRListRow.Create(b, noms.GetValueOrDefault(b.FournisseurId) ?? string.Empty, devise, _locale));
            ApplySearchFilter();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewBr()
    {
        var vm = _sp.GetRequiredService<BREditViewModel>();
        vm.Load(null);
        _workspace.Open(vm);
    }

    [RelayCommand]
    private void OpenSelected()
    {
        if (Selected == null) return;
        var vm = _sp.GetRequiredService<BREditViewModel>();
        vm.Load(Selected.Br.Id);
        _workspace.Open(vm);
    }

    [RelayCommand]
    private async Task DeleteBrAsync(BRListRow? row, CancellationToken cancellationToken)
    {
        if (row == null) return;
        var item = row.Br;

        if (!await _dialog.ConfirmAsync(_locale.T("BR_DlgShort"), _locale.Tf("BR_ConfirmDelete", item.Numero), cancellationToken))
            return;

        IsBusy = true;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var entity = await db.BonsReception.Include(b => b.Lignes).FirstAsync(b => b.Id == item.Id, cancellationToken);
            await _stock.StripBonReceptionMovementsAsync(db, entity.Id, cancellationToken);
            db.BonsReception.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
            if (Selected?.Br.Id == item.Id)
                Selected = null;
            Items.Remove(row);
            _allRows.Remove(row);
            await _dialog.ShowInfoAsync(_locale.T("BR_DlgShort"), _locale.T("BR_Deleted"), cancellationToken);
        }
        catch (Exception ex)
        {
            await _dialog.ShowErrorAsync(_locale.T("BR_DlgShort"), ex.Message, cancellationToken);
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
            var b = await db.BonsReception.Include(x => x.Lignes).FirstAsync(x => x.Id == Selected.Br.Id, cancellationToken);
            var f = await db.Tiers.AsNoTracking().FirstAsync(t => t.Id == b.FournisseurId, cancellationToken);
            var bytes = await _pdf.BuildBonReceptionPdfAsync(b, DocumentPartyPdfInfo.FromTiers(f), cancellationToken);
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
