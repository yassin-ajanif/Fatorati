using System.Globalization;

namespace GestionCommerciale.Shared.Helpers;

public static class CurrencyHelper
{
    public static string Format(decimal amount, string currencyCode = "MAD")
    {
        var c = CultureInfo.GetCultureInfo("fr-FR");
        return amount.ToString("N2", c) + " " + currencyCode;
    }
}
