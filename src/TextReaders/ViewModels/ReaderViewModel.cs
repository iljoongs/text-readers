using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TextReaders.Models;
using TextReaders.Services;

namespace TextReaders.ViewModels;

public partial class ReaderViewModel : ObservableObject
{
    private readonly IFileService _fileService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private Book? _currentBook;

    [ObservableProperty]
    private FlowDocument? _document;

    [ObservableProperty]
    private string _fontFamilyName;

    [ObservableProperty]
    private double _fontSize;

    [ObservableProperty]
    private double _lineSpacingMultiplier;

    [ObservableProperty]
    private MarginPreset _marginPreset;

    public IReadOnlyList<string> AvailableFontFamilyNames => FontCatalog.AvailableFontFamilyNames;

    public IReadOnlyList<MarginPreset> MarginPresetOptions { get; } = Enum.GetValues<MarginPreset>();

    public ReaderViewModel(IFileService fileService, ISettingsService settingsService)
    {
        _fileService = fileService;
        _settingsService = settingsService;

        var settings = _settingsService.Load();
        // 백킹 필드에 직접 대입해 생성자 초기화 중 OnXxxChanged 훅(재포맷/저장)이 돌지 않도록 한다.
        _fontFamilyName = settings.FontFamilyName;
        _fontSize = settings.FontSize;
        _lineSpacingMultiplier = settings.LineSpacingMultiplier;
        _marginPreset = settings.MarginPreset;
    }

    [RelayCommand]
    private void OpenFile()
    {
        try
        {
            var book = _fileService.OpenBookFromDialog();
            if (book is not null)
            {
                LoadBook(book);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"파일을 여는 중 오류가 발생했습니다.\n{ex.Message}", "text-readers",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void LoadBook(Book book)
    {
        CurrentBook = book;
        Document = BuildFlowDocument(book.Content);
        ApplyDocumentFormatting();
    }

    partial void OnFontFamilyNameChanged(string value) => OnFormattingChanged();

    partial void OnFontSizeChanged(double value) => OnFormattingChanged();

    partial void OnLineSpacingMultiplierChanged(double value) => OnFormattingChanged();

    partial void OnMarginPresetChanged(MarginPreset value) => OnFormattingChanged();

    private void OnFormattingChanged()
    {
        ApplyDocumentFormatting();
        _settingsService.Save(new AppSettings
        {
            FontFamilyName = FontFamilyName,
            FontSize = FontSize,
            LineSpacingMultiplier = LineSpacingMultiplier,
            MarginPreset = MarginPreset,
        });
    }

    private void ApplyDocumentFormatting()
    {
        if (Document is null)
        {
            return;
        }

        Document.FontFamily = new FontFamily(new Uri("pack://application:,,,/"), $"./Assets/Fonts/#{FontFamilyName}");
        Document.FontSize = FontSize;
        Document.PagePadding = new Thickness(GetMarginSize());

        var lineHeight = FontSize * 1.3 * LineSpacingMultiplier;
        foreach (var paragraph in Document.Blocks.OfType<Paragraph>())
        {
            paragraph.LineHeight = lineHeight;
        }
    }

    private double GetMarginSize() => MarginPreset switch
    {
        MarginPreset.Narrow => 24,
        MarginPreset.Wide => 80,
        _ => 48,
    };

    private static FlowDocument BuildFlowDocument(string content)
    {
        var document = new FlowDocument();

        var paragraphBlocks = Regex.Split(content, @"\r?\n\s*\r?\n");
        foreach (var block in paragraphBlocks)
        {
            var normalized = Regex.Replace(block, @"\r?\n", " ").Trim();
            if (normalized.Length == 0)
            {
                continue;
            }

            document.Blocks.Add(new Paragraph(new Run(normalized)));
        }

        return document;
    }
}
