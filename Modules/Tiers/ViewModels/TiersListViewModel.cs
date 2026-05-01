using System.Collections.ObjectModel;
using System.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using GestionCommerciale.Modules.Tiers.Models;

using GestionCommerciale.Shared.Database;

using GestionCommerciale.Shared.Services;

using GestionCommerciale.Shared.ViewModels;

using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.DependencyInjection;



namespace GestionCommerciale.Modules.Tiers.ViewModels;



public partial class TiersListViewModel : BaseViewModel

{

    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    private readonly WorkspaceNavigator _workspace;

    private readonly IServiceProvider _sp;
    private readonly ILocaleService _locale;

    private CancellationTokenSource? _filterLoadCts;

    private int _loadGeneration;



    private TiersListScope _scope = TiersListScope.Clients;



    public TiersListViewModel(

        IDbContextFactory<AppDbContext> dbFactory,

        WorkspaceNavigator workspaceNavigator,

        IServiceProvider sp,

        ILocaleService locale)

    {

        _dbFactory = dbFactory;

        _workspace = workspaceNavigator;

        _sp = sp;

        _locale = locale;

        _locale.CultureApplied += (_, _) =>
        {
            Configure(_scope);
            RefreshListUi();
        };
        Configure(TiersListScope.Clients);
        RefreshListUi();
    }

    [ObservableProperty] private string _wmSearch = string.Empty;
    [ObservableProperty] private string _btnNew = string.Empty;
    [ObservableProperty] private string _btnToggleActif = string.Empty;
    [ObservableProperty] private string _colNom = string.Empty;
    [ObservableProperty] private string _colIce = string.Empty;
    [ObservableProperty] private string _colVille = string.Empty;
    [ObservableProperty] private string _colActif = string.Empty;

    private void RefreshListUi()
    {
        WmSearch = _locale.T("Wm_SearchTiers");
        BtnNew = _locale.T("Btn_New");
        BtnToggleActif = _locale.T("Btn_ToggleActif");
        ColNom = _locale.T("Lbl_ColNom");
        ColIce = _locale.T("Lbl_ColIce");
        ColVille = _locale.T("Lbl_ColVille");
        ColActif = _locale.T("Lbl_ColActif");
    }



    public TiersListScope Scope => _scope;



    public void Configure(TiersListScope scope)

    {

        _scope = scope;

        Title = scope == TiersListScope.Clients ? _locale.T("TiersList_Clients") : _locale.T("TiersList_Fournisseurs");

    }



    public ObservableCollection<GestionCommerciale.Modules.Tiers.Models.Tiers> Items { get; } = [];



    [ObservableProperty] private string _filter = string.Empty;

    [ObservableProperty] private GestionCommerciale.Modules.Tiers.Models.Tiers? _selected;



    partial void OnFilterChanged(string value)

    {

        _filterLoadCts?.Cancel();

        _filterLoadCts?.Dispose();

        _filterLoadCts = new CancellationTokenSource();

        _ = LoadItemsAsync(_filterLoadCts.Token);

    }



    [RelayCommand]

    private Task LoadAsync(CancellationToken cancellationToken) => LoadItemsAsync(cancellationToken);



    private async Task LoadItemsAsync(CancellationToken cancellationToken)

    {

        var generation = Interlocked.Increment(ref _loadGeneration);

        IsBusy = true;

        try

        {

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var q = db.Tiers.AsNoTracking().AsQueryable();

            // Inline filter — EF Core cannot translate a call to a local/static method into SQL.

            q = _scope switch

            {

                TiersListScope.Clients => q.Where(t => t.Type == TypeTiers.Client || t.Type == TypeTiers.LesDeux),

                TiersListScope.Fournisseurs => q.Where(t => t.Type == TypeTiers.Fournisseur || t.Type == TypeTiers.LesDeux),

                _ => q

            };

            var f = Filter.Trim();

            if (!string.IsNullOrEmpty(f))

            {

                q = q.Where(t =>

                    t.Nom.Contains(f) || t.ICE.Contains(f) || t.Ville.Contains(f));

            }



            var list = await q.OrderBy(t => t.Nom).ToListAsync(cancellationToken);

            if (generation != _loadGeneration)

                return;

            Items.Clear();

            foreach (var t in list) Items.Add(t);

        }

        finally

        {

            if (generation == _loadGeneration)

                IsBusy = false;

        }

    }



    [RelayCommand]

    private void OpenNew()

    {

        var vm = _sp.GetRequiredService<TiersDetailViewModel>();

        vm.Load(null, _scope);

        _workspace.Open(vm);

    }



    [RelayCommand]

    private void OpenSelected()

    {

        if (Selected == null) return;

        var vm = _sp.GetRequiredService<TiersDetailViewModel>();

        vm.Load(Selected.Id, _scope);

        _workspace.Open(vm);

    }



    [RelayCommand]

    private async Task ToggleActifAsync(CancellationToken cancellationToken)

    {

        if (Selected == null) return;

        IsBusy = true;

        try

        {

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var t = await db.Tiers.FirstAsync(x => x.Id == Selected.Id, cancellationToken);

            t.Actif = !t.Actif;

            await db.SaveChangesAsync(cancellationToken);

            await LoadItemsAsync(cancellationToken);

        }

        finally

        {

            IsBusy = false;

        }

    }

}


