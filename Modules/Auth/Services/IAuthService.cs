using GestionCommerciale.Modules.Auth.Models;

namespace GestionCommerciale.Modules.Auth.Services;

public interface IAuthService
{
    Task<User?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
}
