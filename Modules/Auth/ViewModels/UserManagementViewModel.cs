using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionCommerciale.Modules.Auth.Models;
using GestionCommerciale.Modules.Auth.Services;
using GestionCommerciale.Shared.Database;
using GestionCommerciale.Shared.Services;
using GestionCommerciale.Shared.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace GestionCommerciale.Modules.Auth.ViewModels;

public partial class UserManagementViewModel : BaseViewModel
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IDialogService _dialog;
    private readonly ICurrentUserSession _session;
    private readonly ILocaleService _locale;

    public UserManagementViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        IDialogService dialog,
        ICurrentUserSession session,
        ILocaleService locale)
    {
        _dbFactory = dbFactory;
        _dialog = dialog;
        _session = session;
        _locale = locale;
        _locale.CultureApplied += (_, _) => RefreshUserUi();
        RefreshUserUi();
    }

    [ObservableProperty] private string _btnRefresh = string.Empty;
    [ObservableProperty] private string _btnSave = string.Empty;
    [ObservableProperty] private string _formHint = string.Empty;
    [ObservableProperty] private string _wmNom = string.Empty;
    [ObservableProperty] private string _wmEmailField = string.Empty;
    [ObservableProperty] private string _wmPasswordOptional = string.Empty;

    private void RefreshUserUi()
    {
        Title = _locale.T("Users_Title");
        BtnRefresh = _locale.T("Btn_Refresh");
        BtnSave = _locale.T("Btn_Save");
        FormHint = _locale.T("Users_FormHint");
        WmNom = _locale.T("Wm_Nom");
        WmEmailField = _locale.T("Wm_Email");
        WmPasswordOptional = _locale.T("Wm_PasswordOptional");
    }

    public ObservableCollection<User> Users { get; } = [];

    [ObservableProperty] private string _nom = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private Role _role = Role.Commercial;
    [ObservableProperty] private User? _selectedUser;

    public bool IsAdmin => _session.IsAdmin;

    public Array Roles => Enum.GetValues(typeof(Role));

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var list = await db.Users.AsNoTracking().OrderBy(u => u.Nom).ToListAsync(cancellationToken);
            Users.Clear();
            foreach (var u in list) Users.Add(u);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveUserAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Nom) || string.IsNullOrWhiteSpace(Email))
        {
            await _dialog.ShowErrorAsync(_locale.T("Dlg_Validation"), _locale.T("Users_ErrNameEmail"), cancellationToken);
            return;
        }

        IsBusy = true;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            if (SelectedUser == null)
            {
                if (string.IsNullOrWhiteSpace(Password))
                {
                    await _dialog.ShowErrorAsync(_locale.T("Dlg_Validation"), _locale.T("Users_ErrPwdNew"), cancellationToken);
                    return;
                }

                db.Users.Add(new User
                {
                    Nom = Nom.Trim(),
                    Email = Email.Trim(),
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                    Role = Role,
                    Actif = true
                });
            }
            else
            {
                var u = await db.Users.FirstAsync(x => x.Id == SelectedUser.Id, cancellationToken);
                u.Nom = Nom.Trim();
                u.Email = Email.Trim();
                u.Role = Role;
                if (!string.IsNullOrWhiteSpace(Password))
                    u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);
            }

            await db.SaveChangesAsync(cancellationToken);
            Password = string.Empty;
            await LoadAsync(cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedUserChanged(User? value)
    {
        if (value == null)
        {
            Nom = string.Empty;
            Email = string.Empty;
            Password = string.Empty;
            Role = Role.Commercial;
            return;
        }

        Nom = value.Nom;
        Email = value.Email;
        Password = string.Empty;
        Role = value.Role;
    }
}
