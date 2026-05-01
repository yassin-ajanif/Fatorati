using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionCommerciale.Modules.Facturation.Models;
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

    public FactureListViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        WorkspaceNavigator workspaceNavigator,
        IServiceProvider sp,
        IDialogService dialog,
        IPdfService pdf,
        ILocaleService locale)
    {
        _dbFactory = dbFactory;
        _workspace = workspaceNavigator;
        _sp = sp;
        _dialog = dialog;
        _pdf = pdf;
        _locale = locale;
        _locale.CultureApplied += (_, _) => RefreshListToolbar();
        RefreshListToolbar();
        Title = _locale.T("FactList_Title");
    }

    [ObservableProperty] private string _btnRefresh = string.Empty;
    [ObservableProperty] private string _btnNew = string.Empty;
    [ObservableProperty] private string _btnExportCsv = string.Empty;
    [ObservableProperty] private string _btnPdf = string.Empty;
    [ObservableProperty] private string _wmFilterStatut = string.Empty;

    private void RefreshListToolbar()
    {
        BtnRefresh = _locale.T("Btn_Refresh");
        BtnNew = _locale.T("Btn_NewFacture");
        BtnExportCsv = _locale.T("Export_CsvPicker");
        BtnPdf = _locale.T("Btn_Pdf");
        WmFilterStatut = _locale.T("Wm_FilterStatut");
    }

    public ObservableCollection<Facture> Items { get; } = [];
    [ObservableProperty] private Facture? _selected;
    [ObservableProperty] private StatutFacture? _filterStatut;

    public Array Statuts => Enum.GetValues(typeof(StatutFacture));

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var q = db.Factures.AsNoTracking().Include(f => f.Lignes).AsQueryable();
            if (FilterStatut.HasValue)
                q = q.Where(f => f.Statut == FilterStatut);
            var list = await q.OrderByDescending(f => f.Date).Take(300).ToListAsync(cancellationToken);
            Items.Clear();
            foreach (var f in list) Items.Add(f);
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
        vm.Load(Selected.Id);
        _workspace.Open(vm);
    }

    [RelayCommand]
    private async Task ExportCsvAsync(CancellationToken cancellationToken)
    {
        var path = await _dialog.PickSaveFileAsync(_locale.T("Export_CsvPicker"), "factures.csv", new[] { "*.csv" }, cancellationToken);
        if (path == null) return;
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var list = await db.Factures.AsNoTracking().OrderByDescending(f => f.Date).Take(2000).ToListAsync(cancellationToken);
        await using var w = new StreamWriter(path);
        await w.WriteLineAsync("Numero;Date;ClientId;Statut");
        foreach (var f in list)
            await w.WriteLineAsync($"{f.Numero};{f.Date:yyyy-MM-dd};{f.ClientId};{f.Statut}");
        await _dialog.ShowInfoAsync(_locale.T("Export_Csv"), _locale.T("Export_Done"), cancellationToken);
    }

    [RelayCommand]
    private async Task ExportPdfAsync(CancellationToken cancellationToken)
    {
        if (Selected == null) return;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var f = await db.Factures.Include(x => x.Lignes).Include(x => x.Paiements).FirstAsync(x => x.Id == Selected.Id, cancellationToken);
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
