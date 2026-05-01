using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using GestionCommerciale.Infrastructure;
using GestionCommerciale.Modules.Auth.Services;
using GestionCommerciale.Modules.Auth.ViewModels;
using GestionCommerciale.Shared.Database;
using GestionCommerciale.Shared.Services;
using GestionCommerciale.ViewModels;
using GestionCommerciale.Views;
using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestionCommerciale;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            BindingPlugins.DataValidators.RemoveAt(0);

            var sc = new ServiceCollection();
            sc.AddGestionCommerciale();
            Services = sc.BuildServiceProvider();

            using (var db = Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext())
            {
                try
                {
                    db.Database.Migrate();
                }
                catch (SqliteException ex) when (
                    ex.SqliteErrorCode == 1 &&
                    ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                {
                    // Some local databases were created with schema objects present but missing
                    // migration history entries. In that case, allow startup to continue.
                }

                EnsureSocieteMentionsLegalesColumn(db);

                DbSeeder.Seed(db);
            }

            Services.GetRequiredService<ILocaleService>().InitializeAsync().GetAwaiter().GetResult();

            var mainVm = Services.GetRequiredService<MainWindowViewModel>();
            var root = Services.GetRequiredService<RootNavigator>();
            var auth = Services.GetRequiredService<IAuthService>();
            var user = auth.LoginAsync(DbSeeder.DefaultAdminEmail, DbSeeder.DefaultAdminPassword, default)
                .GetAwaiter()
                .GetResult();
            root.SetRoot(user != null
                ? Services.GetRequiredService<AppShellViewModel>()
                : Services.GetRequiredService<LoginViewModel>());

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Repairs SQLite DBs where the model expects SocieteMentionsLegales but Migrate did not add it.</summary>
    private static void EnsureSocieteMentionsLegalesColumn(AppDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        var wasClosed = conn.State != ConnectionState.Open;
        if (wasClosed) conn.Open();
        try
        {
            using var check = conn.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('AppSettings') WHERE name = 'SocieteMentionsLegales'";
            var n = Convert.ToInt64(check.ExecuteScalar() ?? 0L);
            if (n > 0) return;

            using (var alter = conn.CreateCommand())
            {
                alter.CommandText = "ALTER TABLE AppSettings ADD COLUMN SocieteMentionsLegales TEXT NULL;";
                alter.ExecuteNonQuery();
            }

            try
            {
                using var hist = conn.CreateCommand();
                hist.CommandText =
                    "INSERT OR IGNORE INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") " +
                    "VALUES ('20260430220000_AddSocieteMentionsLegales', '9.0.0');";
                hist.ExecuteNonQuery();
            }
            catch
            {
                // No migrations table (e.g. legacy EnsureCreated DB); column is enough for runtime.
            }
        }
        finally
        {
            if (wasClosed && conn.State == ConnectionState.Open)
                conn.Close();
        }
    }
}
