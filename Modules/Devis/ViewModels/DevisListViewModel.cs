using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionCommerciale.Modules.Devis.Models;
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

    public DevisListViewModel(
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
        Title = _locale.T("DevisList_Title");
    }

    [ObservableProperty] private string _btnRefresh = string.Empty;
    [ObservableProperty] private string _btnNew = string.Empty;
    [ObservableProperty] private string _btnPdf = string.Empty;

    private void RefreshListToolbar()
    {
        BtnRefresh = _locale.T("Btn_Refresh");
        BtnNew = _locale.T("Btn_New");
        BtnPdf = _locale.T("Btn_Pdf");
    }

    public ObservableCollection<GestionCommerciale.Modules.Devis.Models.Devis> Items { get; } = [];
    [ObservableProperty] private GestionCommerciale.Modules.Devis.Models.Devis? _selected;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var list = await db.Devis.AsNoTracking().Include(d => d.Lignes).OrderByDescending(d => d.Date).Take(200).ToListAsync(cancellationToken);
            Items.Clear();
            foreach (var d in list) Items.Add(d);
        }
        finally
        {
            IsBusy = false;
        }
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
        await vm.LoadAsync(sel.Id, cancellationToken);
        _workspace.Open(vm);
    }

    [RelayCommand]
    private async Task ExportPdfAsync(CancellationToken cancellationToken)
    {
        if (Selected == null) return;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var d = await db.Devis.Include(x => x.Lignes).FirstAsync(x => x.Id == Selected.Id, cancellationToken);
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
