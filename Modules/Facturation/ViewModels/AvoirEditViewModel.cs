using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionCommerciale.Modules.Auth.Services;
using GestionCommerciale.Modules.Stock;
using GestionCommerciale.Modules.Facturation.Models;
using GestionCommerciale.Modules.Facturation.Services;
using GestionCommerciale.Shared.Database;
using GestionCommerciale.Shared.Services;
using GestionCommerciale.Shared.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestionCommerciale.Modules.Facturation.ViewModels;

public partial class AvoirLineRow : ObservableObject
{
    [ObservableProperty] private int _produitId;
    [ObservableProperty] private string _designation = string.Empty;
    [ObservableProperty] private decimal _quantite = 1;
    [ObservableProperty] private decimal _prixUnitaireHt;
    [ObservableProperty] private decimal _tauxTva;
}

public partial class AvoirEditViewModel : BaseViewModel
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IDocumentNumberService _numbers;
    private readonly IAvoirWorkflowService _workflow;
    private readonly IDialogService _dialog;
    private readonly WorkspaceNavigator _workspace;
    private readonly IServiceProvider _sp;
    private readonly ICurrentUserSession _session;
    private readonly ILocaleService _locale;

    public AvoirEditViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        IDocumentNumberService numbers,
        IAvoirWorkflowService workflow,
        IDialogService dialog,
        WorkspaceNavigator workspaceNavigator,
        IServiceProvider sp,
        ICurrentUserSession session,
        ILocaleService locale)
    {
        _dbFactory = dbFactory;
        _numbers = numbers;
        _workflow = workflow;
        _dialog = dialog;
        _workspace = workspaceNavigator;
        _sp = sp;
        _session = session;
        _locale = locale;
        _locale.CultureApplied += (_, _) => RefreshAvoirUi();
        Title = _locale.T("Avoir_Title");
        RefreshAvoirUi();
    }

    [ObservableProperty] private string _btnBackFacture = string.Empty;
    [ObservableProperty] private string _lblDateAvoir = string.Empty;
    [ObservableProperty] private string _wmMotif = string.Empty;
    [ObservableProperty] private string _chkRetourStock = string.Empty;
    [ObservableProperty] private string _btnAddLine = string.Empty;
    [ObservableProperty] private string _btnApplyLastProduct = string.Empty;
    [ObservableProperty] private string _btnSave = string.Empty;
    [ObservableProperty] private string _btnValidateAvoir = string.Empty;

    private void RefreshAvoirUi()
    {
        BtnBackFacture = _locale.T("Lbl_BackFacture");
        LblDateAvoir = _locale.T("Lbl_DateAvoir");
        WmMotif = _locale.T("Lbl_Motif");
        ChkRetourStock = _locale.T("Lbl_ReturnStock");
        BtnAddLine = _locale.T("Btn_AddLine");
        BtnApplyLastProduct = _locale.T("Btn_ApplyLastProduct");
        BtnSave = _locale.T("Btn_Save");
        BtnValidateAvoir = _locale.T("Btn_ValidateAvoir");
    }

    public ObservableCollection<GestionCommerciale.Modules.Stock.Models.Produit> Produits { get; } = [];
    public ObservableCollection<AvoirLineRow> Lignes { get; } = [];

    [ObservableProperty] private int? _avoirId;
    [ObservableProperty] private int? _factureId;
    [ObservableProperty] private int _clientId;
    [ObservableProperty] private string _numero = string.Empty;
    [ObservableProperty] private DateTimeOffset _date = new(DateTime.Today);
    [ObservableProperty] private string _motif = string.Empty;
    [ObservableProperty] private bool _retourMarchandise;

    public void Load(int? id)
    {
        if (id == null)
            _ = LoadNewAsync(CancellationToken.None);
        else
            _ = LoadExistingAsync(id.Value, CancellationToken.None);
    }

    public void LoadNew(int factureId) => _ = LoadNewAsync(factureId, CancellationToken.None);

    private async Task LoadNewAsync(CancellationToken cancellationToken)
    {
        if (!_session.CanAccessAvoir)
        {
            await _dialog.ShowErrorAsync(_locale.T("Avoir_Title"), _locale.T("Avoir_ErrDenied"), cancellationToken);
            return;
        }

        AvoirId = null;
        FactureId = null;
        ClientId = 0;
        Lignes.Clear();
        Numero = _locale.T("Avoir_DraftPlaceholder");
        Date = new DateTimeOffset(DateTime.Today);
        Motif = string.Empty;
        RetourMarchandise = false;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var produits = await db.Produits.AsNoTracking().Where(p => p.Actif)
            .SelectForListWithoutImageData().ToListAsync(cancellationToken);
        Produits.Clear();
        foreach (var p in produits) Produits.Add(p);
        Title = _locale.T("Avoir_NewTitle");
    }

    private async Task LoadNewAsync(int factureId, CancellationToken cancellationToken)
    {
        if (!_session.CanAccessAvoir)
        {
            await _dialog.ShowErrorAsync(_locale.T("Avoir_Title"), _locale.T("Avoir_ErrDenied"), cancellationToken);
            return;
        }

        AvoirId = null;
        FactureId = factureId;
        Lignes.Clear();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var f = await db.Factures.Include(x => x.Lignes).FirstAsync(x => x.Id == factureId, cancellationToken);
        ClientId = f.ClientId;
        Numero = _locale.T("Avoir_DraftPlaceholder");
        foreach (var l in f.Lignes)
        {
            Lignes.Add(new AvoirLineRow
            {
                ProduitId = l.ProduitId,
                Designation = l.Designation,
                Quantite = Math.Min(l.Quantite, 1),
                PrixUnitaireHt = l.PrixUnitaireHT,
                TauxTva = l.TauxTVA
            });
        }

        var produits = await db.Produits.AsNoTracking().Where(p => p.Actif)
            .SelectForListWithoutImageData().ToListAsync(cancellationToken);
        Produits.Clear();
        foreach (var p in produits) Produits.Add(p);
        Title = _locale.T("Avoir_NewTitle");
    }

    public void LoadExisting(int avoirId) => _ = LoadExistingAsync(avoirId, CancellationToken.None);

    private async Task LoadExistingAsync(int avoirId, CancellationToken cancellationToken)
    {
        if (!_session.CanAccessAvoir)
        {
            await _dialog.ShowErrorAsync(_locale.T("Avoir_Title"), _locale.T("Avoir_ErrDenied"), cancellationToken);
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var avoir = await db.Avoirs.Include(x => x.Lignes).FirstAsync(x => x.Id == avoirId, cancellationToken);
        AvoirId = avoir.Id;
        FactureId = avoir.FactureId;
        ClientId = avoir.ClientId;
        Numero = avoir.Numero;
        Date = new DateTimeOffset(avoir.Date);
        Motif = avoir.Motif;
        RetourMarchandise = avoir.RetourMarchandise;
        Lignes.Clear();
        foreach (var l in avoir.Lignes)
        {
            Lignes.Add(new AvoirLineRow
            {
                ProduitId = l.ProduitId,
                Designation = l.Designation,
                Quantite = l.Quantite,
                PrixUnitaireHt = l.PrixUnitaireHT,
                TauxTva = l.TauxTVA
            });
        }

        var produits = await db.Produits.AsNoTracking().Where(p => p.Actif)
            .SelectForListWithoutImageData().ToListAsync(cancellationToken);
        Produits.Clear();
        foreach (var p in produits) Produits.Add(p);
        Title = _locale.Tf("Avoir_TitleNum", Numero);
    }

    [RelayCommand]
    private void AddLine()
    {
        var p = Produits.FirstOrDefault();
        var row = new AvoirLineRow
        {
            ProduitId = p?.Id ?? 0,
            Designation = p?.Designation ?? string.Empty,
            Quantite = 1,
            PrixUnitaireHt = p?.PrixVenteHT ?? 0,
            TauxTva = p?.TauxTVA ?? 20
        };
        Lignes.Add(row);
    }

    [RelayCommand]
    private void ApplyProductLast()
    {
        var row = Lignes.LastOrDefault();
        if (row == null) return;
        var p = Produits.FirstOrDefault(x => x.Id == row.ProduitId);
        if (p == null) return;
        row.Designation = p.Designation;
        row.PrixUnitaireHt = p.PrixVenteHT;
        row.TauxTva = p.TauxTVA;
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!_session.CanAccessAvoir) return;
        if (!Lignes.Any())
        {
            await _dialog.ShowErrorAsync(_locale.T("Avoir_Title"), _locale.T("Avoir_ErrLines"), cancellationToken);
            return;
        }

        IsBusy = true;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var num = await _numbers.NextAvoirAsync(cancellationToken);
            var avoir = new Avoir
            {
                Numero = num,
                FactureId = FactureId,
                ClientId = ClientId,
                Date = Date.DateTime,
                Motif = Motif,
                RetourMarchandise = RetourMarchandise,
                CreatedByUserId = _session.UserId
            };
            foreach (var l in Lignes)
            {
                avoir.Lignes.Add(new AvoirLigne
                {
                    ProduitId = l.ProduitId,
                    Designation = l.Designation,
                    Quantite = l.Quantite,
                    PrixUnitaireHT = l.PrixUnitaireHt,
                    TauxTVA = l.TauxTva
                });
            }

            db.Avoirs.Add(avoir);
            await db.SaveChangesAsync(cancellationToken);
            AvoirId = avoir.Id;
            Numero = avoir.Numero;
            await _dialog.ShowInfoAsync(_locale.T("Avoir_Title"), _locale.T("Avoir_Saved"), cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ValiderAsync(CancellationToken cancellationToken)
    {
        if (AvoirId == null) return;
        try
        {
            IsBusy = true;
            await _workflow.CreerEtValiderAsync(AvoirId.Value, _session.UserId, cancellationToken);
            await _dialog.ShowInfoAsync(_locale.T("Avoir_Title"), _locale.T("Avoir_Validated"), cancellationToken);
        }
        catch (Exception ex)
        {
            await _dialog.ShowErrorAsync(_locale.T("Avoir_Title"), ex.Message, cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Back()
    {
        var f = _sp.GetRequiredService<FactureEditViewModel>();
        f.Load(FactureId);
        _workspace.Open(f);
    }
}
