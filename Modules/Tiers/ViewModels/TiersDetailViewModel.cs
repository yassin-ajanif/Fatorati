using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using GestionCommerciale.Modules.Tiers.Models;
using GestionCommerciale.Modules.Devis.Models;
using GestionCommerciale.Modules.Facturation.Models;
using GestionCommerciale.Modules.Livraison.Models;
using GestionCommerciale.Modules.Reception.Models;

using GestionCommerciale.Shared.Database;

using GestionCommerciale.Shared.Services;

using GestionCommerciale.Shared.ViewModels;

using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.DependencyInjection;



namespace GestionCommerciale.Modules.Tiers.ViewModels;



public partial class TiersDetailViewModel : BaseViewModel

{

    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    private readonly IDialogService _dialog;

    private readonly WorkspaceNavigator _workspace;

    private readonly IServiceProvider _sp;

    private readonly ILocaleService _locale;

    private TiersListScope _returnScope = TiersListScope.Clients;

    /// <summary>List context (clients vs fournisseurs) for shell nav highlight.</summary>
    public TiersListScope ListScope => _returnScope;

    public TiersDetailViewModel(

        IDbContextFactory<AppDbContext> dbFactory,

        IDialogService dialog,

        WorkspaceNavigator workspaceNavigator,

        IServiceProvider sp,

        ILocaleService locale)

    {

        _dbFactory = dbFactory;

        _dialog = dialog;

        _workspace = workspaceNavigator;

        _sp = sp;

        _locale = locale;

        Title = _locale.T("TiersDetail_Title");

        RebuildTypeOptions();
        _locale.CultureApplied += (_, _) =>
        {
            RefreshDetailUi();
            if (TiersId.HasValue)
                _ = LoadAsync(TiersId.Value, CancellationToken.None);
        };
        RefreshDetailUi();
    }

    [ObservableProperty] private string _btnBackList = string.Empty;
    [ObservableProperty] private string _wmNom = string.Empty;
    [ObservableProperty] private string _wmIce = string.Empty;
    [ObservableProperty] private string _wmAdresse = string.Empty;
    [ObservableProperty] private string _wmVille = string.Empty;
    [ObservableProperty] private string _wmTelephone = string.Empty;
    [ObservableProperty] private string _wmEmail = string.Empty;
    [ObservableProperty] private string _wmConditions = string.Empty;
    [ObservableProperty] private string _chkActif = string.Empty;
    [ObservableProperty] private string _btnSave = string.Empty;
    [ObservableProperty] private string _lblHistorique = string.Empty;

    private void RefreshDetailUi()
    {
        BtnBackList = _locale.T("Btn_BackList");
        WmNom = _locale.T("Wm_Nom");
        WmIce = _locale.T("Wm_Ice");
        WmAdresse = _locale.T("Wm_Adresse");
        WmVille = _locale.T("Wm_Ville");
        WmTelephone = _locale.T("Wm_Telephone");
        WmEmail = _locale.T("Wm_Email");
        WmConditions = _locale.T("Wm_ConditionsPaiement");
        ChkActif = _locale.T("Lbl_Actif");
        BtnSave = _locale.T("Btn_Save");
        LblHistorique = _locale.T("Lbl_HistoryDocs");
    }



    public ObservableCollection<string> Historique { get; } = [];



    public ObservableCollection<TypeTiers> Types { get; } = [];



    [ObservableProperty] private int? _tiersId;

    [ObservableProperty] private TypeTiers _type = TypeTiers.Client;

    [ObservableProperty] private string _nom = string.Empty;

    [ObservableProperty] private string _ice = string.Empty;

    [ObservableProperty] private string _adresse = string.Empty;

    [ObservableProperty] private string _ville = string.Empty;

    [ObservableProperty] private string _telephone = string.Empty;

    [ObservableProperty] private string _email = string.Empty;

    [ObservableProperty] private string _conditionsPaiement = string.Empty;

    [ObservableProperty] private bool _actif = true;



    private void RebuildTypeOptions()

    {

        Types.Clear();

        switch (_returnScope)

        {

            case TiersListScope.Clients:

                Types.Add(TypeTiers.Client);

                Types.Add(TypeTiers.LesDeux);

                break;

            case TiersListScope.Fournisseurs:

                Types.Add(TypeTiers.Fournisseur);

                Types.Add(TypeTiers.LesDeux);

                break;

        }

    }



    public void Load(int? tiersId) => Load(tiersId, TiersListScope.Clients);



    public void Load(int? tiersId, TiersListScope returnScope)

    {

        _returnScope = returnScope;

        RebuildTypeOptions();

        TiersId = tiersId;

        Historique.Clear();
        RefreshDetailUi();

        if (tiersId == null)

        {

            Nom = string.Empty;

            Ice = string.Empty;

            Adresse = string.Empty;

            Ville = string.Empty;

            Telephone = string.Empty;

            Email = string.Empty;

            ConditionsPaiement = string.Empty;

            Type = returnScope == TiersListScope.Fournisseurs ? TypeTiers.Fournisseur : TypeTiers.Client;

            Actif = true;

            Title = returnScope == TiersListScope.Fournisseurs ? _locale.T("TiersDetail_NewSupplier") : _locale.T("TiersDetail_NewClient");

            return;

        }



        _ = LoadAsync(tiersId.Value, CancellationToken.None);

    }



    private async Task LoadAsync(int id, CancellationToken cancellationToken)

    {

        IsBusy = true;

        try

        {

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var t = await db.Tiers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (t == null) return;



            Type = t.Type;

            if (!Types.Contains(Type))

            {

                Types.Add(Type);

            }



            Nom = t.Nom;

            Ice = t.ICE;

            Adresse = t.Adresse;

            Ville = t.Ville;

            Telephone = t.Telephone;

            Email = t.Email;

            ConditionsPaiement = t.ConditionsPaiement;

            Actif = t.Actif;

            Title = _returnScope == TiersListScope.Fournisseurs

                ? _locale.Tf("Tiers_TitleSupplierFmt", t.Nom)

                : _locale.Tf("Tiers_TitleClientFmt", t.Nom);



            Historique.Clear();

            var isClient = t.Type is TypeTiers.Client or TypeTiers.LesDeux;

            var isFourn = t.Type is TypeTiers.Fournisseur or TypeTiers.LesDeux;



            if (isClient)

            {

                var devisRows = await db.Devis.AsNoTracking().Where(d => d.ClientId == id).OrderByDescending(d => d.Date).Take(50)
                    .Select(d => new { d.Numero, d.Date }).ToListAsync(cancellationToken);
                foreach (var d in devisRows)
                    Historique.Add(_locale.Tf("Hist_LineFmtShort", _locale.T("Hist_Devis"), d.Numero, d.Date.ToString("d")));

                var blRows = await db.BonsLivraison.AsNoTracking().Where(b => b.ClientId == id).OrderByDescending(b => b.Date).Take(50)
                    .Select(b => new { b.Numero, b.Date }).ToListAsync(cancellationToken);
                foreach (var b in blRows)
                    Historique.Add(_locale.Tf("Hist_LineFmtShort", _locale.T("Hist_BL"), b.Numero, b.Date.ToString("d")));

                var facRows = await db.Factures.AsNoTracking().Where(f => f.ClientId == id).OrderByDescending(f => f.Date).Take(50)
                    .Select(f => new { f.Numero, f.Date, f.EstPayee }).ToListAsync(cancellationToken);
                foreach (var f in facRows)
                    Historique.Add(_locale.Tf("Hist_LineFmt", _locale.T("Hist_Facture"), f.Numero, f.Date.ToString("d"),
                        _locale.T(f.EstPayee ? "Fact_Paid" : "Fact_Unpaid")));

            }



            if (isFourn)

            {

                var brRows = await db.BonsReception.AsNoTracking().Where(b => b.FournisseurId == id).OrderByDescending(b => b.Date).Take(50)
                    .Select(b => new { b.Numero, b.Date }).ToListAsync(cancellationToken);
                foreach (var b in brRows)
                    Historique.Add(_locale.Tf("Hist_LineFmtShort", _locale.T("Hist_BR"), b.Numero, b.Date.ToString("d")));

            }

        }

        finally

        {

            IsBusy = false;

        }

    }



    [RelayCommand]

    private async Task SaveAsync(CancellationToken cancellationToken)

    {

        if (string.IsNullOrWhiteSpace(Nom))

        {

            await _dialog.ShowErrorAsync(_locale.T("Dlg_Validation"), _locale.T("Tiers_ErrName"), cancellationToken);

            return;

        }



        IsBusy = true;

        try

        {

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            if (TiersId == null)

            {

                var t = new GestionCommerciale.Modules.Tiers.Models.Tiers

                {

                    Type = Type,

                    Nom = Nom.Trim(),

                    ICE = Ice.Trim(),

                    Adresse = Adresse.Trim(),

                    Ville = Ville.Trim(),

                    Telephone = Telephone.Trim(),

                    Email = Email.Trim(),

                    ConditionsPaiement = ConditionsPaiement.Trim(),

                    Actif = Actif

                };

                db.Tiers.Add(t);

                await db.SaveChangesAsync(cancellationToken);

                TiersId = t.Id;

            }

            else

            {

                var t = await db.Tiers.FirstAsync(x => x.Id == TiersId, cancellationToken);

                t.Type = Type;

                t.Nom = Nom.Trim();

                t.ICE = Ice.Trim();

                t.Adresse = Adresse.Trim();

                t.Ville = Ville.Trim();

                t.Telephone = Telephone.Trim();

                t.Email = Email.Trim();

                t.ConditionsPaiement = ConditionsPaiement.Trim();

                t.Actif = Actif;

                await db.SaveChangesAsync(cancellationToken);

            }



            await _dialog.ShowInfoAsync(_locale.T("Tiers_InfoTitle"), _locale.T("Tiers_Saved"), cancellationToken);

            if (TiersId.HasValue)

                await LoadAsync(TiersId.Value, cancellationToken);

        }

        finally

        {

            IsBusy = false;

        }

    }



    [RelayCommand]

    private void Back()

    {

        var list = _sp.GetRequiredService<TiersListViewModel>();

        list.Configure(_returnScope);

        _workspace.Open(list);

    }

}


