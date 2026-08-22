namespace TextReaders.Models;

public static class FontCatalog
{
    public const string DefaultFontFamilyName = "Noto Serif KR";

    public static IReadOnlyList<string> AvailableFontFamilyNames { get; } = new[]
    {
        "Noto Serif KR",
        "NanumMyeongjo",
        "RIDIBatang",
        "NanumBarunGothic",
        "Spoqa Han Sans Neo",
    };
}
