namespace BlogCms.Web.Configuration;

/// <summary>
/// Feature-Flags zur Steuerung der UI-Sichtbarkeit einzelner Bereiche.
/// Bindet an die Konfigurationssektion <see cref="SectionName"/> ("Features"),
/// die über appsettings*.json, Umgebungsvariablen (z. B.
/// <c>Features__MonetizationEnabled</c>) oder die lokal geladene <c>.env</c>-Datei
/// gesetzt werden kann. Standard ist jeweils aktiviert.
/// </summary>
public class FeatureOptions
{
    public const string SectionName = "Features";

    /// <summary>
    /// Steuert die Sichtbarkeit der Monetarisierungs-UI (Mitgliedschaft, Spenden,
    /// Premium-CTAs). Bei <c>false</c> werden diese Elemente nicht gerendert; die
    /// zugehörigen Seiten bleiben per Direkt-URL erreichbar.
    /// </summary>
    public bool MonetizationEnabled { get; set; } = true;

    /// <summary>
    /// Steuert die Sichtbarkeit der Login-/Registrierungs-UI (Anmelden- und
    /// Registrieren-Links sowie Anmelde-Hinweise). Bei <c>false</c> werden diese
    /// Elemente nicht gerendert; <c>/Account/Login</c> bleibt per Direkt-URL erreichbar.
    /// </summary>
    public bool LoginEnabled { get; set; } = true;
}
