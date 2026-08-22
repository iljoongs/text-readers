namespace TextReaders.Models;

public sealed class AppSettings
{
    public string FontFamilyName { get; set; } = FontCatalog.DefaultFontFamilyName;

    public double FontSize { get; set; } = 20;

    public double LineSpacingMultiplier { get; set; } = 1.0;

    public MarginPreset MarginPreset { get; set; } = MarginPreset.Normal;

    public WindowGeometry? Window { get; set; }
}
