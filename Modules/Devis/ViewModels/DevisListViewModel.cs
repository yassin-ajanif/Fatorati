using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionCommerciale.Shared.Helpers;
using GestionCommerciale.Shared.Database;
using GestionCommerciale.Shared.Models.Pdf;
using GestionCommerciale.Shared.Services;
using GestionCommerciale.Shared.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestionCommerciale.Modules.Devis.ViewModels;

public partial class DevisListViewModel : BaseViewModel
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly WorkspaceNavigator _workspace;
    private readonly IServiceProvider _sp;
    private readonly IDialogService _dialog;
    private readonly IPdfService _pdf;
    private readonly ILocaleService _locale;
    private readonly IAppSettingsService _settings;

    public DevisListViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        WorkspaceNavigator workspaceNavigator,
        IServiceProvider sp,
        IDialogService dialog,
        IPdfService pdf,
        ILocaleService locale,
        IAppSettingsService settings)
    {
        _dbFactory = dbFactory;
        _workspace = workspaceNavigator;
        _sp = sp;
        _dialog = dialog;
        _pdf = pdf;
        _locale = locale;
        _settings = settings;
        _locale.CultureApplied += (_, _) => RefreshListToolbar();
        RefreshListToolbar();
        Title = _locale.T("DevisList_Title");
    }

    [ObservableProperty] private string _btnRefresh = string.Empty;
    [ObservableProperty] private string _btnNew = string.Empty;
    [ObservableProperty] private string _btnPdf = string.Empty;
    [ObservableProperty] private string _btnFilterDate = string.Empty;
    [ObservableProperty] private string _menuDeleteDevis = string.Empty;
    private DateTime? _dateFrom;
    private DateTime? _dateTo;
    [ObservableProperty] private string _colHeaderRef = string.Empty;
    [ObservableProperty] private string _colHeaderClient = string.Empty;
    [ObservableProperty] private string _colHeaderDate = string.Empty;
    [ObservableProperty] private string _colHeaderValidite = string.Empty;
    [ObservableProperty] private string _colHeaderHt = string.Empty;
    [ObservableProperty] private string _colHeaderTtc = string.Empty;
    [ObservableProperty] private string _colHeaderNote = string.Empty;
    [ObservableProperty] private string _searchWatermark = string.Empty;

    private readonly List<DevisListRow> _allRows = [];

    private void RefreshListToolbar()
    {
        BtnRefresh = _locale.T("Btn_Refresh");
        BtnNew = _locale.T("Btn_New");
        BtnPdf = _locale.T("Btn_Pdf");
        UpdateBtnFilterDateText();
        MenuDeleteDevis = _locale.T("Devis_MenuDelete");
        ColHeaderRef = _locale.T("DevisList_ColRef");
        ColHeaderClient = _locale.T("Lbl_Client");
        ColHeaderDate = _locale.T("DevisList_ColDate");
        ColHeaderValidite = _locale.T("DevisList_ColValidite");
        ColHeaderHt = _locale.T("DevisList_ColHt");
        ColHeaderTtc = _locale.T("DevisList_ColTtc");
        ColHeaderNote = _locale.T("DevisList_ColNote");
        SearchWatermark = _locale.T("DocList_SearchPlaceholderClient");
    }

    public ObservableCollection<DevisListRow> Items { get; } = [];
    [ObservableProperty] private DevisListRow? _selected;
    [ObservableProperty] private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value) => ApplySearchFilter();

    private void ApplySearchFilter()
    {
        var selId = Selected?.Devis.Id;
        Items.Clear();
        foreach (var r in _allRows)
        {
            if (DocumentListFilter.Matches(SearchText, r.Devis.Numero, r.ClientNom))
                Items.Add(r);
        }
        if (selId is { } id)
            Selected = Items.FirstOrDefault(x => x.Devis.Id == id);
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
            var q = db.Devis.AsNoTracking().Include(d => d.Lignes).AsQueryable();
            if (_dateFrom.HasValue)
                q = q.Where(d => d.Date >= _dateFrom.Value);
            if (_dateTo.HasValue)
                q = q.Where(d => d.Date <= _dateTo.Value);
            var list = await q.OrderByDescending(d => d.Date).Take(200).ToListAsync(cancellationToken);
            var clientIds = list.Select(d => d.ClientId).Distinct().ToList();
            var noms = await db.Tiers.AsNoTracking()
                .Where(t => clientIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Nom, cancellationToken);
            _allRows.Clear();
            foreach (var d in list)
            {
                var nom = noms.GetValueOrDefault(d.ClientId) ?? string.Empty;
                _allRows.Add(DevisListRow.Create(d, nom, devise, _locale));
            }
            ApplySearchFilter();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateBtnFilterDateText()
    {
        if (_dateFrom.HasValue && _dateTo.HasValue)
            BtnFilterDate = $"{_dateFrom:dd/MM/yy} — {_dateTo:dd/MM/yy}";
        else
            BtnFilterDate = _locale.T("Btn_FilterDate");
    }

    [RelayCommand]
    private async Task FilterDateAsync(CancellationToken cancellationToken)
    {
        var range = await _dialog.PickDateRangeAsync(_locale.T("Btn_FilterDate"), cancellationToken);
        if (range == null) return;
        if (range.Value.from == DateTime.MinValue && range.Value.to == DateTime.MinValue)
        {
            _dateFrom = null;
            _dateTo = null;
        }
        else
        {
            _dateFrom = range.Value.from;
            _dateTo = range.Value.to;
        }
        UpdateBtnFilterDateText();
        await LoadAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task NewDevisAsync(CancellationToken cancellationToken)
    {
        var vm = _sp.GetRequiredService<DevisEditViewModel>();
        await vm.LoadAsync(null, cancellationToken);
        _workspace.Open(vm);
    }

    [RelayCommand]
    private async Task OpenSelectedAsync(CancellationToken cancellationToken)
    {
        var sel = Selected;
        if (sel == null) return;
        var vm = _sp.GetRequiredService<DevisEditViewModel>();
        await vm.LoadAsync(sel.Devis.Id, cancellationToken);
        _workspace.Open(vm);
    }

    [RelayCommand]
    private async Task DeleteDevisAsync(DevisListRow? row, CancellationToken cancellationToken)
    {
        if (row == null) return;
        var item = row.Devis;

        if (!await _dialog.ConfirmAsync(_locale.T("Devis_Title"), _locale.Tf("Devis_ConfirmDelete", item.Numero), cancellationToken))
            return;

        IsBusy = true;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var blockedMsg = await DevisDeleteReferencedMessage.BuildIfBlockedAsync(db, item.Id, _locale, cancellationToken);
            if (blockedMsg != null)
            {
                await _dialog.ShowErrorAsync(_locale.T("Devis_Title"), blockedMsg, cancellationToken);
                return;
            }

            var entity = await db.Devis.Include(d => d.Lignes).FirstAsync(d => d.Id == item.Id, cancellationToken);
            db.Devis.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
            if (Selected?.Devis.Id == item.Id)
                Selected = null;
            Items.Remove(row);
            _allRows.Remove(row);
            await _dialog.ShowInfoAsync(_locale.T("Devis_Title"), _locale.T("Devis_Deleted"), cancellationToken);
        }
        catch (Exception ex)
        {
            await _dialog.ShowErrorAsync(_locale.T("Devis_Title"), ex.Message, cancellationToken);
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
            var d = await db.Devis.Include(x => x.Lignes).FirstAsync(x => x.Id == Selected.Devis.Id, cancellationToken);
            var client = await db.Tiers.AsNoTracking().FirstAsync(t => t.Id == d.ClientId, cancellationToken);
            var bytes = await _pdf.BuildDevisPdfAsync(d, DocumentPartyPdfInfo.FromTiers(client), cancellationToken);
            var ok = await _dialog.SavePickedFileBytesAsync(_locale.T("Export_PdfPicker"), $"{d.Numero}.pdf", new[] { "*.pdf" }, bytes, cancellationToken);
            if (ok)
                await _dialog.ShowInfoAsync(_locale.T("Export_Pdf"), _locale.T("Export_Done"), cancellationToken);
        }
        catch (Exception ex)
        {
            await _dialog.ShowErrorAsync(_locale.T("Export_Pdf"), ex.Message, cancellationToken);
        }
    }
}
