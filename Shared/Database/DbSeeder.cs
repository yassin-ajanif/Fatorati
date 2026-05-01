using GestionCommerciale.Modules.Auth.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionCommerciale.Shared.Database;

public static class DbSeeder
{
    public const string DefaultAdminEmail = "admin@local";
    public const string DefaultAdminPassword = "admin";

    public static void Seed(AppDbContext db)
    {
        if (!db.AppSettings.Any())
        {
            db.AppSettings.Add(new AppSettingsRow { Id = 1 });
            db.SaveChanges();
        }

        if (!db.Users.Any())
        {
            db.Users.Add(new User
            {
                Nom = "Administrateur",
                Email = DefaultAdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultAdminPassword),
                Role = Role.Admin,
                Actif = true
            });
            db.SaveChanges();
        }
    }
}
