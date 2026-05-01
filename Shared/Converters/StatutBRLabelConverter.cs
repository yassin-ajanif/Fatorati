using System;
using System.Globalization;
using Avalonia.Data.Converters;
using GestionCommerciale.Modules.Reception.Models;
using GestionCommerciale.Shared.Services;

namespace GestionCommerciale.Shared.Converters;

public sealed class StatutBRLabelConverter : IValueConverter
{
    public static readonly StatutBRLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not StatutBR s)
            return value?.ToString() ?? string.Empty;
        var lang = culture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "fr";
        return UiTranslations.Get(s == StatutBR.Valide ? "StatutBR_Valide" : "StatutBR_Brouillon", lang);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
