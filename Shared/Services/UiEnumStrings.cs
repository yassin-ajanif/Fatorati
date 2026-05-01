using GestionCommerciale.Modules.Facturation.Models;

namespace GestionCommerciale.Shared.Services;

public static class UiEnumStrings
{
    public static string FormatModePaiement(ILocaleService locale, ModePaiement m) =>
        locale.T(m switch
        {
            ModePaiement.Virement => "ModePaiement_Virement",
            ModePaiement.Cheque => "ModePaiement_Cheque",
            ModePaiement.Especes => "ModePaiement_Especes",
            ModePaiement.Carte => "ModePaiement_Carte",
            _ => "ModePaiement_Virement"
        });
}
