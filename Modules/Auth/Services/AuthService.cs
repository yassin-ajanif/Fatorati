using GestionCommerciale.Modules.Auth.Models;
using GestionCommerciale.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace GestionCommerciale.Modules.Auth.Services;

public sealed class AuthService : IAuthService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentUserSession _session;

    public AuthService(IDbContextFactory<AppDbContext> dbFactory, ICurrentUserSession session)
    {
        _dbFactory = dbFactory;
        _session = session;
    }

    public async Task<User?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email && u.Actif, cancellationToken);
        if (user == null) return null;
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return null;

        _session.SetSession(user);
        return user;
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        _session.Clear();
        return Task.CompletedTask;
    }
}
