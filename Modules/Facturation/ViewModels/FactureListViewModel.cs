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

namespace GestionCommerciale.Modules.Facturation.ViewModels;

public partial class FactureListViewModel : BaseViewModel
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly WorkspaceNavigator _workspace;
    private readonly IServiceProvider _sp;
    private readonly IDialogService _dialog;
    private readonly IPdfService _pdf;
    private readonly ILocaleService _locale;
    private readonly IAppSettingsService _settings;

    public FactureListViewModel(
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
        Title = _locale.T("FactList_Title");
    }

    [ObservableProperty] private string _btnRefresh = string.Empty;
    [ObservableProperty] private string _btnNew = string.Empty;
    [ObservableProperty] private string _btnPdf = string.Empty;
    [ObservableProperty] private string _wmFilterPayee = string.Empty;
    [ObservableProperty] private string _menuDeleteFacture = string.Empty;
    [ObservableProperty] private string _colHeaderRef = string.Empty;
    [ObservableProperty] private string _colHeaderParty = string.Empty;
    [ObservableProperty] private string _colHeaderDate = string.Empty;
    [ObservableProperty] private string _colHeaderEcheance = string.Empty;
    [ObservableProperty] private string _colHeaderPayee = string.Empty;
    [ObservableProperty] private string _colHeaderHt = string.Empty;
    [ObservableProperty] private string _colHeaderTtc = string.Empty;
    [ObservableProperty] private string _colHeaderNote = string.Empty;
    [ObservableProperty] private string _searchWatermark = string.Empty;
    [ObservableProperty] private string _lblPayeeFilterAll = string.Empty;
    [ObservableProperty] private string _lblPayeeFilterUnpaid = string.Empty;
    [ObservableProperty] private string _lblPayeeFilterPaid = string.Empty;

    private readonly List<FactureListRow> _allRows = [];

    private void RefreshListToolbar()
    {
        BtnRefresh = _locale.T("Btn_Refresh");
        BtnNew = _locale.T("Btn_NewFacture");
        BtnPdf = _locale.T("Btn_Pdf");
        WmFilterPayee = _locale.T("Fact_FilterPayee");
        LblPayeeFilterAll = _locale.T("Fact_FilterAll");
        LblPayeeFilterUnpaid = _locale.T("Fact_Unpaid");
        LblPayeeFilterPaid = _locale.T("Fact_Paid");
        MenuDeleteFacture = _locale.T("Fact_MenuDelete");
        ColHeaderRef = _locale.T("DevisList_ColRef");
        ColHeaderParty = _locale.T("Lbl_Client");
        ColHeaderDate = _locale.T("DevisList_ColDate");
        ColHeaderEcheance = _locale.T("DocList_ColEcheance");
        ColHeaderPayee = _locale.T("FactList_ColPayee");
        ColHeaderHt = _locale.T("DevisList_ColHt");
        ColHeaderTtc = _locale.T("DevisList_ColTtc");
        ColHeaderNote = _locale.T("DevisList_ColNote");
        SearchWatermark = _locale.T("DocList_SearchPlaceholderClient");
    }

    public ObservableCollection<FactureListRow> Items { get; } = [];
    [ObservableProperty] private FactureListRow? _selected;
    /// <summary>0 = all, 1 = unpaid, 2 = paid.</summary>
    [ObservableProperty] private int _payeeFilterIndex;
    [ObservableProperty] private string _searchText = string.Empty;

    partial void OnPayeeFilterIndexChanged(int value) => _ = LoadAsync(CancellationToken.None);

    partial void OnSearchTextChanged(string value) => ApplySearchFilter();

    private void ApplySearchFilter()
    {
        var selId = Selected?.Facture.Id;
        Items.Clear();
        foreach (var r in _allRows)
        {
            if (DocumentListFilter.Matches(SearchText, r.Facture.Numero, r.ClientNom))
                Items.Add(r);
        }
        if (selId is { } id)
            Selected = Items.FirstOrDefault(x => x.Facture.Id == id);
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
            var q = db.Factures.AsNoTracking().Include(f => f.Lignes).AsQueryable();
            q = PayeeFilterIndex switch
            {
                1 => q.Where(f => !f.EstPayee),
                2 => q.Where(f => f.EstPayee),
                _ => q,
            };
            var list = await q.OrderByDescending(f => f.Date).Take(300).ToListAsync(cancellationToken);
            var ids = list.Select(f => f.ClientId).Distinct().ToList();
            var noms = await db.Tiers.AsNoTracking()
                .Where(t => ids.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Nom, cancellationToken);
            _allRows.Clear();
            foreach (var f in list)
                _allRows.Add(FactureListRow.Create(f, noms.GetValueOrDefault(f.ClientId) ?? string.Empty, devise, _locale));
            ApplySearchFilter();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewFacture()
    {
        var vm = _sp.GetRequiredService<FactureEditViewModel>();
        vm.Load(null);
        _workspace.Open(vm);
    }

    [RelayCommand]
    private void OpenSelected()
    {
        if (Selected == null) return;
        var vm = _sp.GetRequiredService<FactureEditViewModel>();
        vm.Load(Selected.Facture.Id);
        _workspace.Open(vm);
    }

    [RelayCommand]
    private async Task DeleteFactureAsync(FactureListRow? row, CancellationToken cancellationToken)
    {
        if (row == null) return;
        var item = row.Facture;

        if (!await _dialog.ConfirmAsync(_locale.T("Fact_Title"), _locale.Tf("Fact_ConfirmDelete", item.Numero), cancellationToken))
            return;

        IsBusy = true;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            if (await db.Avoirs.AsNoTracking().AnyAsync(a => a.FactureId == item.Id, cancellationToken))
            {
                await _dialog.ShowErrorAsync(_locale.T("Fact_Title"), _locale.T("Fact_ErrDeleteReferenced"), cancellationToken);
                return;
            }

            var entity = await db.Factures.Include(f => f.Lignes).Include(f => f.Paiements).FirstAsync(f => f.Id == item.Id, cancellationToken);
            db.Factures.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);

            if (Selected?.Facture.Id == item.Id)
                Selected = null;
            Items.Remove(row);
            _allRows.Remove(row);
            await _dialog.ShowInfoAsync(_locale.T("Fact_Title"), _locale.T("Fact_Deleted"), cancellationToken);
        }
        catch (Exception ex)
        {
            await _dialog.ShowErrorAsync(_locale.T("Fact_Title"), ex.Message, cancellationToken);
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
            var f = await db.Factures.Include(x => x.Lignes).Include(x => x.Paiements).FirstAsync(x => x.Id == Selected.Facture.Id, cancellationToken);
            var client = await db.Tiers.AsNoTracking().FirstAsync(t => t.Id == f.ClientId, cancellationToken);
            var bytes = await _pdf.BuildFacturePdfAsync(f, DocumentPartyPdfInfo.FromTiers(client), cancellationToken);
            var ok = await _dialog.SavePickedFileBytesAsync(_locale.T("Export_PdfPicker"), $"{f.Numero}.pdf", new[] { "*.pdf" }, bytes, cancellationToken);
            if (ok)
                await _dialog.ShowInfoAsync(_locale.T("Export_Pdf"), _locale.T("Export_Done"), cancellationToken);
        }
        catch (Exception ex)
        {
            await _dialog.ShowErrorAsync(_locale.T("Export_Pdf"), ex.Message, cancellationToken);
        }
    }
}
