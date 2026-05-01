using System;
using System.Globalization;
using Avalonia.Data.Converters;
using GestionCommerciale.Modules.Facturation.Models;
using GestionCommerciale.Shared.Services;

namespace GestionCommerciale.Shared.Converters;

public sealed class StatutFactureLabelConverter : IValueConverter
{
    public static readonly StatutFactureLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not StatutFacture s)
            return value?.ToString() ?? string.Empty;
        var lang = culture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "fr";
        var key = s switch
        {
            StatutFacture.Brouillon => "StatutFacture_Brouillon",
            StatutFacture.Emise => "StatutFacture_Emise",
            StatutFacture.PartiellementPayee => "StatutFacture_PartiellementPayee",
            StatutFacture.Payee => "StatutFacture_Payee",
            StatutFacture.Annulee => "StatutFacture_Annulee",
            _ => "StatutFacture_Brouillon"
        };
        return UiTranslations.Get(key, lang);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
