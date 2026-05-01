namespace GestionCommerciale.Modules.Facturation.Models;

public enum StatutFacture
{
    /// <summary>Legacy DB value; new factures are saved as <see cref="Emise"/>.</summary>
    Brouillon,
    Emise,
    PartiellementPayee,
    Payee,
    Annulee
}
