using System;
using System.Globalization;
using Avalonia.Data.Converters;
using GestionCommerciale.Modules.Livraison.Models;
using GestionCommerciale.Shared.Services;

namespace GestionCommerciale.Shared.Converters;

public sealed class StatutBLLabelConverter : IValueConverter
{
    public static readonly StatutBLLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not StatutBL s)
            return value?.ToString() ?? string.Empty;
        var lang = culture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "fr";
        var key = s switch
        {
            StatutBL.Brouillon => "StatutBL_Brouillon",
            StatutBL.Valide => "StatutBL_Valide",
            StatutBL.Livre => "StatutBL_Livre",
            StatutBL.Facture => "StatutBL_Facture",
            _ => "StatutBL_Brouillon"
        };
        return UiTranslations.Get(key, lang);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
