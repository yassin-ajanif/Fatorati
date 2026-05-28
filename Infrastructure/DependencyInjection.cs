using GestionCommerciale.Modules.Auth.Services;
using GestionCommerciale.Modules.Auth.ViewModels;
using GestionCommerciale.Modules.Devis.ViewModels;
using GestionCommerciale.Modules.Facturation.Services;
using GestionCommerciale.Modules.Facturation.ViewModels;
using GestionCommerciale.Modules.Livraison.Services;
using GestionCommerciale.Modules.Livraison.ViewModels;
using GestionCommerciale.Modules.Commande.ViewModels;
using GestionCommerciale.Modules.Reception.Services;
using GestionCommerciale.Modules.Reception.ViewModels;
using GestionCommerciale.Modules.Reporting.ViewModels;
using GestionCommerciale.Modules.Stock.Services;
using GestionCommerciale.Modules.Stock.ViewModels;
using GestionCommerciale.Modules.Tiers.ViewModels;
using GestionCommerciale.Shared.Database;
using GestionCommerciale.Shared.Services;
using GestionCommerciale.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestionCommerciale.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGestionCommerciale(this IServiceCollection services)
    {
        var cs = DatabasePath.GetConnectionString();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(cs));

        services.AddSingleton<RootNavigator>();
        services.AddSingleton<IRootNavigator>(sp => sp.GetRequiredService<RootNavigator>());
        services.AddSingleton<WorkspaceNavigator>();
        services.AddSingleton<IWorkspaceNavigator>(sp => sp.GetRequiredService<WorkspaceNavigator>());
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ICurrentUserSession, CurrentUserSession>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IUiPreferencesService, UiPreferencesService>();
        services.AddSingleton<ILocaleService, LocaleService>();
        services.AddSingleton<IDocumentNumberService, DocumentNumberService>();
        services.AddSingleton<IStockMovementService, StockMovementService>();
        services.AddSingleton<IBonLivraisonWorkflowService, BonLivraisonWorkflowService>();
        services.AddSingleton<IBonReceptionWorkflowService, BonReceptionWorkflowService>();
        services.AddSingleton<IFactureWorkflowService, FactureWorkflowService>();
        services.AddSingleton<IAvoirWorkflowService, AvoirWorkflowService>();
        services.AddSingleton<ILicenseService, LicenseService>();
        services.AddSingleton<IPdfService, PdfService>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<AppShellViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<TiersListViewModel>();
        services.AddTransient<TiersDetailViewModel>();
        services.AddTransient<StockMainViewModel>();
        services.AddTransient<ProduitsViewModel>();
        services.AddTransient<DevisListViewModel>();
        services.AddTransient<DevisEditViewModel>();
        services.AddTransient<BLListViewModel>();
        services.AddTransient<BLEditViewModel>();
        services.AddTransient<BRListViewModel>();
        services.AddTransient<BREditViewModel>();
        services.AddTransient<BCListViewModel>();
        services.AddTransient<BCEditViewModel>();
        services.AddTransient<FactureListViewModel>();
        services.AddTransient<FactureEditViewModel>();
        services.AddTransient<AvoirEditViewModel>();
        services.AddTransient<ReportingViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services;
    }
}
