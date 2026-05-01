using GestionCommerciale.Modules.Auth.Services;
using GestionCommerciale.Modules.Reporting.ViewModels;
using GestionCommerciale.Shared.Services;
using GestionCommerciale.Shared.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GestionCommerciale.Modules.Auth.ViewModels;

public class HomeViewModel : BaseViewModel
{
    private readonly ILocaleService _locale;

    public HomeViewModel(ILocaleService locale, ICurrentUserSession session, IServiceProvider sp)
    {
        _locale = locale;
        Title = _locale.T("Nav_Home");
        _locale.CultureApplied += (_, _) =>
        {
            OnPropertyChanged(nameof(Welcome));
            Title = _locale.T("Nav_Home");
        };

        if (session.CanAccessReporting)
            Dashboard = sp.GetRequiredService<ReportingViewModel>();
    }

    /// <summary>Reporting dashboard; null when the user role cannot access reporting.</summary>
    public ReportingViewModel? Dashboard { get; }

    public bool ShowDashboard => Dashboard is not null;

    public bool ShowWelcomeOnly => Dashboard is null;

    public string Welcome => _locale.T("Home_Welcome");
}
