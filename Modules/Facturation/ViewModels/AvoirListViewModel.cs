using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionCommerciale.Modules.Auth.Services;
using GestionCommerciale.Modules.Facturation.Models;
using GestionCommerciale.Shared.Database;
using GestionCommerciale.Shared.Helpers;
using GestionCommerciale.Shared.Models.Pdf;
using GestionCommerciale.Shared.Services;
using GestionCommerciale.Shared.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestionCommerciale.Modules.Facturation.ViewModels;

public partial class AvoirListViewModel : BaseViewModel
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly WorkspaceNavigator _workspace;
    private readonly IServiceProvider _sp;
    private readonly IDialogService _dialog;
    private readonly IPdfService _pdf;
    private readonly ILocaleService _locale;
    private readonly ICurrentUserSession _session;

    public AvoirListViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        WorkspaceNavigator workspaceNavigator,
        IServiceProvider sp,
        IDialogService dialog,
        IPdfService pdf,
        ILocaleService locale,
        ICurrentUserSession session)
    {
        _dbFactory = dbFactory;
        _workspace = workspaceNavigator;
        _sp = sp;
        _dialog = dialog;
        _pdf = pdf;
        _locale = locale;
        _session = session;
        _locale.CultureApplied += (_, _) =>
        {
            RefreshListToolbar();
            LoadCommand.Execute(null);
        };
        RefreshListToolbar();
        Title = _locale.T("AvoirList_Title");
    }

    [ObservableProperty] private string _btnRefresh = string.Empty;
    [ObservableProperty] private string _btnNew = string.Empty;
    [ObservableProperty] private string _btnPdf = string.Empty;
    [ObservableProperty] private string _btnFilterDate = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;
    private DateTime? _dateFrom;
    private DateTime? _dateTo;
    [ObservableProperty] private string _wmSearch = string.Empty;
    [ObservableProperty] private string _colNumero = string.Empty;
    [ObservableProperty] private string _colClient = string.Empty;
    [ObservableProperty] private string _colDate = string.Empty;
    [ObservableProperty] private string _colFacture = string.Empty;
    [ObservableProperty] private string _colMotif = string.Empty;
    [ObservableProperty] private string _colHt = string.Empty;
    [ObservableProperty] private string _colTtc = string.Empty;

    private void RefreshListToolbar()
    {
        BtnRefresh = _locale.T("Btn_Refresh");
        BtnNew = _locale.T("Btn_NewAvoir");
        BtnPdf = _locale.T("Btn_Pdf");
        UpdateBtnFilterDateText();
        WmSearch = _locale.T("Wm_SearchAvoirList");
        // Reuse existing document/list header keys so the UI shows real labels (not missing key strings).
        ColNumero = _locale.T("DevisList_ColRef");
        ColClient = _locale.T("Lbl_Client");
        ColDate = _locale.T("DevisList_ColDate");
        ColFacture = _locale.T("DocList_ColFacture");
        ColMotif = _locale.T("Lbl_Motif");
        ColHt = _locale.T("DevisList_ColHt");
        ColTtc = _locale.T("DevisList_ColTtc");
    }

    partial void OnSearchTextChanged(string value) => LoadCommand.Execute(null);

    public ObservableCollection<AvoirListRow> Items { get; } = [];
    [ObservableProperty] private AvoirListRow? _selected;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (!_session.CanAccessAvoir)
        {
            Items.Clear();
            return;
        }

        IsBusy = true;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var cfg = await db.AppSettings.AsNoTracking().FirstAsync(cancellationToken);
            var devise = string.IsNullOrWhiteSpace(cfg.Devise) ? "MAD" : cfg.Devise.Trim();

            var joined = from a in db.Avoirs.AsNoTracking().Include(a => a.Lignes)
                          join t in db.Tiers.AsNoTracking() on a.ClientId equals t.Id into tj
                          from t in tj.DefaultIfEmpty()
                          join f in db.Factures.AsNoTracking() on a.FactureId equals f.Id into fj
                          from f in fj.DefaultIfEmpty()
                          select new { a, nom = t != null ? t.Nom : string.Empty, factNum = f != null ? f.Numero : string.Empty };

            var joinedQ = joined.AsQueryable();
            if (_dateFrom.HasValue)
                joinedQ = joinedQ.Where(x => x.a.Date >= _dateFrom.Value);
            if (_dateTo.HasValue)
                joinedQ = joinedQ.Where(x => x.a.Date <= _dateTo.Value);

            List<(Avoir a, string nom, string factNum)> raw;
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                var rows = await joinedQ
                    .OrderByDescending(x => x.a.Date)
                    .Take(DocumentNumberSearchHelper.ResultCap)
                    .Select(x => new { x.a, nom = x.nom, factNum = x.factNum })
                    .ToListAsync(cancellationToken);
                raw = rows.Select(r => (r.a, r.nom, r.factNum)).ToList();
            }
            else
            {
                var term = SearchText.Trim();
                if (DocumentNumberSearchHelper.IsNumericSearchTerm(term))
                {
                    var rows = await joinedQ
                        .OrderByDescending(x => x.a.Date)
                        .Take(DocumentNumberSearchHelper.NumericScanCap)
                        .Select(x => new { x.a, nom = x.nom, factNum = x.factNum })
                        .ToListAsync(cancellationToken);
                    raw = rows
                        .Where(r => DocumentNumberSearchHelper.MatchesNumeroAndParty(r.a.Numero, r.nom, term)
                            || DocumentNumberSearchHelper.MatchesNumeroAndParty(r.factNum, string.Empty, term))
                        .Take(DocumentNumberSearchHelper.ResultCap)
                        .Select(r => (r.a, r.nom, r.factNum))
                        .ToList();
                }
                else
                {
                    var filtered = joinedQ.Where(x =>
                        x.a.Numero.Contains(term)
                        || x.nom.Contains(term)
                        || x.factNum.Contains(term)
                        || (x.a.Motif ?? string.Empty).Contains(term));
                    var textRows = await filtered
                        .OrderByDescending(x => x.a.Date)
                        .Take(DocumentNumberSearchHelper.ResultCap)
                        .Select(x => new { x.a, nom = x.nom, factNum = x.factNum })
                        .ToListAsync(cancellationToken);
                    raw = textRows.Select(r => (r.a, r.nom, r.factNum)).ToList();
                }
            }

            var selId = Selected?.Avoir.Id;
            Items.Clear();
            foreach (var (a, nom, fn) in raw)
                Items.Add(AvoirListRow.Create(a, nom, fn, devise, _locale));
            if (selId is { } id)
                Selected = Items.FirstOrDefault(i => i.Avoir.Id == id);
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
    private void NewAvoir()
    {
        if (!_session.CanAccessAvoir) return;
        var vm = _sp.GetRequiredService<AvoirEditViewModel>();
        vm.Load(null);
        _workspace.Open(vm);
    }

    [RelayCommand]
    private void OpenSelected()
    {
        if (Selected == null || !_session.CanAccessAvoir) return;
        var vm = _sp.GetRequiredService<AvoirEditViewModel>();
        vm.LoadExisting(Selected.Avoir.Id);
        _workspace.Open(vm);
    }

    [RelayCommand]
    private async Task ExportPdfAsync(CancellationToken cancellationToken)
    {
        if (Selected == null || !_session.CanAccessAvoir) return;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var a = await db.Avoirs.Include(x => x.Lignes).FirstAsync(x => x.Id == Selected.Avoir.Id, cancellationToken);
            var client = await db.Tiers.AsNoTracking().FirstAsync(t => t.Id == a.ClientId, cancellationToken);
            var bytes = await _pdf.BuildAvoirPdfAsync(a, DocumentPartyPdfInfo.FromTiers(client), cancellationToken);
            var ok = await _dialog.SavePickedFileBytesAsync(_locale.T("Export_PdfPicker"), $"{a.Numero}.pdf", new[] { "*.pdf" }, bytes, cancellationToken);
            if (ok)
                await _dialog.ShowInfoAsync(_locale.T("Export_Pdf"), _locale.T("Export_Done"), cancellationToken);
        }
        catch (Exception ex)
        {
            await _dialog.ShowErrorAsync(_locale.T("Export_Pdf"), ex.Message, cancellationToken);
        }
    }
}
