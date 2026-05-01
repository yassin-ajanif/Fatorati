using System;
using System.Globalization;
using Avalonia.Data.Converters;
using GestionCommerciale.Modules.Commande.Models;
using GestionCommerciale.Shared.Services;

namespace GestionCommerciale.Shared.Converters;

public sealed class StatutBCLabelConverter : IValueConverter
{
    public static readonly StatutBCLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not StatutBC s)
            return value?.ToString() ?? string.Empty;
        var lang = culture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "fr";
        return UiTranslations.Get(s == StatutBC.Valide ? "StatutBC_Valide" : "StatutBC_Brouillon", lang);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
