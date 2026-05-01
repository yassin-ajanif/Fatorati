using GestionCommerciale.Shared.Models;

namespace GestionCommerciale.Modules.Auth.Models;

public class User : BaseEntity
{
    public string Nom { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Role Role { get; set; }
    public bool Actif { get; set; } = true;
}
