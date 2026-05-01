using GestionCommerciale.Modules.Commande.Models;
using GestionCommerciale.Modules.Facturation.Models;
using GestionCommerciale.Modules.Livraison.Models;
using GestionCommerciale.Modules.Reception.Models;

namespace GestionCommerciale.Shared.Services;

public static class UiEnumStrings
{
    public static string FormatStatutFacture(ILocaleService locale, StatutFacture s) =>
        locale.T(s switch
        {
            StatutFacture.Brouillon => "StatutFacture_Brouillon",
            StatutFacture.Emise => "StatutFacture_Emise",
            StatutFacture.PartiellementPayee => "StatutFacture_PartiellementPayee",
            StatutFacture.Payee => "StatutFacture_Payee",
            StatutFacture.Annulee => "StatutFacture_Annulee",
            _ => "StatutFacture_Brouillon"
        });

    public static string FormatStatutBL(ILocaleService locale, StatutBL s) =>
        locale.T(s switch
        {
            StatutBL.Brouillon => "StatutBL_Brouillon",
            StatutBL.Valide => "StatutBL_Valide",
            StatutBL.Livre => "StatutBL_Livre",
            StatutBL.Facture => "StatutBL_Facture",
            _ => "StatutBL_Brouillon"
        });

    public static string FormatStatutBC(ILocaleService locale, StatutBC s) =>
        locale.T(s == StatutBC.Valide ? "StatutBC_Valide" : "StatutBC_Brouillon");

    public static string FormatStatutBR(ILocaleService locale, StatutBR s) =>
        locale.T(s == StatutBR.Valide ? "StatutBR_Valide" : "StatutBR_Brouillon");

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
