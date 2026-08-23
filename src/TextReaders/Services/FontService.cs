using System.IO;
using System.Windows.Media;
using Microsoft.Win32;
using TextReaders.Models;

namespace TextReaders.Services;

public sealed class FontService : IFontService
{
    private static readonly string CustomFontsDirectory = Path.Combine(AppContext.BaseDirectory, "data", "CustomFonts");

    // family name -> 해당 폰트가 들어있는 폴더 URI (pack:// 내장 폰트가 아닌, 사용자가 추가한 폰트만 담는다)
    private readonly Dictionary<string, Uri> _customFontFolders = new();

    public FontService()
    {
        ScanCustomFonts();
    }

    public IReadOnlyList<string> GetAvailableFontFamilyNames()
        => FontCatalog.AvailableFontFamilyNames.Concat(_customFontFolders.Keys).ToList();

    public FontFamily ResolveFontFamily(string familyName)
    {
        if (_customFontFolders.TryGetValue(familyName, out var folderUri))
        {
            return new FontFamily(folderUri, $"./#{familyName}");
        }

        return new FontFamily(new Uri("pack://application:,,,/"), $"./Assets/Fonts/#{familyName}");
    }

    public string? AddCustomFontFromDialog()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "폰트 파일 (*.ttf;*.otf)|*.ttf;*.otf",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        return AddCustomFont(dialog.FileName);
    }

    public string AddCustomFont(string sourceFilePath)
    {
        Directory.CreateDirectory(CustomFontsDirectory);
        var destPath = Path.Combine(CustomFontsDirectory, Path.GetFileName(sourceFilePath));
        File.Copy(sourceFilePath, destPath, overwrite: true);

        var familyName = DiscoverFamilyName(destPath)
            ?? throw new InvalidOperationException("폰트 파일에서 글꼴 이름을 찾을 수 없습니다.");

        _customFontFolders[familyName] = new Uri(CustomFontsDirectory + Path.DirectorySeparatorChar);
        return familyName;
    }

    private void ScanCustomFonts()
    {
        if (!Directory.Exists(CustomFontsDirectory))
        {
            return;
        }

        var folderUri = new Uri(CustomFontsDirectory + Path.DirectorySeparatorChar);
        foreach (var file in Directory.EnumerateFiles(CustomFontsDirectory))
        {
            var familyName = DiscoverFamilyName(file);
            if (familyName is not null)
            {
                _customFontFolders[familyName] = folderUri;
            }
        }
    }

    private static string? DiscoverFamilyName(string fontFilePath)
    {
        var family = Fonts.GetFontFamilies(new Uri(fontFilePath)).FirstOrDefault();
        return family?.FamilyNames.Values.FirstOrDefault();
    }
}
