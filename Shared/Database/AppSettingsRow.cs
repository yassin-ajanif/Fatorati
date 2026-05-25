namespace GestionCommerciale.Shared.Database;

/// <summary>Singleton row (Id=1) for application / company settings.</summary>
public class AppSettingsRow
{
    public int Id { get; set; } = 1;
    public string SocieteNom { get; set; } = string.Empty;
    public string SocieteAdresse { get; set; } = string.Empty;
    public string SocieteICE { get; set; } = string.Empty;
    /// <summary>Optional multi-line block (RC, Patente, téléphone, etc.) printed in PDF footer.</summary>
    public string? SocieteMentionsLegales { get; set; }
    public string? SocieteLogoPath { get; set; }
    public string TauxTVAJson { get; set; } = "[20]"; // JSON array of decimals
    public bool BlocageSiStockInsuffisant { get; set; } = true;
    public int DevisValiditeJoursDefaut { get; set; } = 30;
    public string Devise { get; set; } = "MAD";

    /// <summary>Interface language: <c>fr</c> (default) or <c>ar</c> (RTL).</summary>
    public string UiLanguage { get; set; } = "fr";

    /// <summary>Enables the on-screen virtual keyboard for touch-screen POS use.</summary>
    public bool EnableVirtualKeyboard { get; set; }

    /// <summary>UTC date when the 3-day trial was first started (null = not started).</summary>
    public DateTime? TrialStartedAt { get; set; }

    /// <summary>Stored license key once activated (null = not yet licensed).</summary>
    public string? LicenseKey { get; set; }
}
