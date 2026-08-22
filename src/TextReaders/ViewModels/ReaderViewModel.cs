using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TextReaders.Models;
using TextReaders.Services;

namespace TextReaders.ViewModels;

public partial class ReaderViewModel : ObservableObject
{
    private readonly IFileService _fileService;
    private readonly ISettingsService _settingsService;
    private readonly ILibraryService _libraryService;
    private readonly DispatcherTimer _positionSaveTimer;

    [ObservableProperty]
    private Book? _currentBook;

    [ObservableProperty]
    private FlowDocument? _document;

    [ObservableProperty]
    private int _currentPageNumber = 1;

    [ObservableProperty]
    private int _pageCount = 1;

    [ObservableProperty]
    private string _fontFamilyName;

    [ObservableProperty]
    private double _fontSize;

    [ObservableProperty]
    private double _lineSpacingMultiplier;

    [ObservableProperty]
    private MarginPreset _marginPreset;

    [ObservableProperty]
    private ReadingTheme _theme;

    [ObservableProperty]
    private double _dimmingOpacity;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    private int _lastSearchParagraphIndex = -1;

    public event Action<int>? NavigateToPageRequested;

    public event Action<Paragraph>? NavigateToParagraphRequested;

    public IReadOnlyList<string> AvailableFontFamilyNames => FontCatalog.AvailableFontFamilyNames;

    public IReadOnlyList<MarginPreset> MarginPresetOptions { get; } = Enum.GetValues<MarginPreset>();

    public IReadOnlyList<ReadingTheme> ThemeOptions { get; } = Enum.GetValues<ReadingTheme>();

    public Brush PageBackgroundBrush => GetPageBackgroundBrush();

    public string ReadingProgressText => PageCount > 0
        ? $"{CurrentPageNumber} / {PageCount} ({(int)Math.Round(CurrentPageNumber * 100.0 / PageCount)}%)"
        : string.Empty;

    public ObservableCollection<Bookmark> Bookmarks { get; } = new();

    public ObservableCollection<TocEntry> TableOfContents { get; } = new();

    public ReaderViewModel(IFileService fileService, ISettingsService settingsService, ILibraryService libraryService)
    {
        _fileService = fileService;
        _settingsService = settingsService;
        _libraryService = libraryService;

        var settings = _settingsService.Load();
        // 백킹 필드에 직접 대입해 생성자 초기화 중 OnXxxChanged 훅(재포맷/저장)이 돌지 않도록 한다.
        _fontFamilyName = settings.FontFamilyName;
        _fontSize = settings.FontSize;
        _lineSpacingMultiplier = settings.LineSpacingMultiplier;
        _marginPreset = settings.MarginPreset;
        _theme = settings.Theme;
        _dimmingOpacity = settings.DimmingOpacity;

        _positionSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _positionSaveTimer.Tick += (_, _) =>
        {
            _positionSaveTimer.Stop();
            if (CurrentBook is not null)
            {
                _libraryService.UpdatePosition(CurrentBook.FilePath, CurrentPageNumber);
            }
        };
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
        var (document, tocEntries) = BuildFlowDocument(book.Content);
        Document = document;
        ApplyDocumentFormatting();

        TableOfContents.Clear();
        foreach (var entry in tocEntries)
        {
            TableOfContents.Add(entry);
        }

        Bookmarks.Clear();
        foreach (var bookmark in _libraryService.GetBookmarks(book.FilePath))
        {
            Bookmarks.Add(bookmark);
        }

        ApplyHighlights(_libraryService.GetHighlights(book.FilePath));

        _lastSearchParagraphIndex = -1;

        var savedPageIndex = _libraryService.GetLastPageIndex(book.FilePath);
        _libraryService.UpdatePosition(book.FilePath, savedPageIndex);

        if (savedPageIndex > 1)
        {
            // FlowDocumentPageViewer가 새 Document를 레이아웃할 시간을 준 뒤 페이지를 이동한다.
            Application.Current.Dispatcher.BeginInvoke(
                () => NavigateToPageRequested?.Invoke(savedPageIndex),
                DispatcherPriority.ContextIdle);
        }
    }

    [RelayCommand]
    private void AddBookmark()
    {
        if (CurrentBook is null)
        {
            return;
        }

        var bookmark = new Bookmark { PageNumber = CurrentPageNumber, CreatedAt = DateTime.Now };
        _libraryService.AddBookmark(CurrentBook.FilePath, bookmark);
        Bookmarks.Add(bookmark);
    }

    [RelayCommand]
    private void GoToBookmark(Bookmark bookmark) => NavigateToPageRequested?.Invoke(bookmark.PageNumber);

    [RelayCommand]
    private void RemoveBookmark(Bookmark bookmark)
    {
        if (CurrentBook is null)
        {
            return;
        }

        _libraryService.RemoveBookmark(CurrentBook.FilePath, bookmark);
        Bookmarks.Remove(bookmark);
    }

    [RelayCommand]
    private void GoToTocEntry(TocEntry entry) => NavigateToParagraphRequested?.Invoke(entry.Paragraph);

    [RelayCommand]
    private void FindNext()
    {
        if (Document is null || string.IsNullOrWhiteSpace(SearchQuery))
        {
            return;
        }

        var paragraphs = Document.Blocks.OfType<Paragraph>().ToList();
        if (paragraphs.Count == 0)
        {
            return;
        }

        for (var offset = 1; offset <= paragraphs.Count; offset++)
        {
            var index = (_lastSearchParagraphIndex + offset) % paragraphs.Count;
            var text = new TextRange(paragraphs[index].ContentStart, paragraphs[index].ContentEnd).Text;
            if (text.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
            {
                _lastSearchParagraphIndex = index;
                NavigateToParagraphRequested?.Invoke(paragraphs[index]);
                return;
            }
        }

        MessageBox.Show($"'{SearchQuery}'를 찾을 수 없습니다.", "text-readers",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void AddHighlight(int paragraphIndex, int startOffset, int length, string? note)
    {
        if (CurrentBook is null)
        {
            return;
        }

        var highlight = new Highlight
        {
            ParagraphIndex = paragraphIndex,
            StartOffset = startOffset,
            Length = length,
            Note = note,
        };

        _libraryService.AddHighlight(CurrentBook.FilePath, highlight);

        // 같은 문단에 하이라이트가 여러 개 있을 수 있으므로, 그 문단에 속한 전체 하이라이트를
        // 다시 모아서 한 번에 재구성한다 (하나씩 따로 적용하면 뒤에 적용된 것이 앞의 것의
        // Inlines.Clear()에 의해 지워진다).
        ApplyHighlights(_libraryService.GetHighlights(CurrentBook.FilePath));
    }

    // 하이라이트 구간을 별도 Run으로 잘라내 배경색(및 메모가 있으면 ToolTip)을 입힌다.
    // 문단은 매 LoadBook마다 새로 만들어지므로, 저장된 하이라이트는 로드할 때마다 다시 적용해야 한다.
    // 문단별로 묶어 한 번에 재구성해야 같은 문단 안의 여러 하이라이트가 서로를 지우지 않는다.
    private void ApplyHighlights(IEnumerable<Highlight> highlights)
    {
        var paragraphs = Document?.Blocks.OfType<Paragraph>().ToList();
        if (paragraphs is null)
        {
            return;
        }

        foreach (var group in highlights.GroupBy(h => h.ParagraphIndex))
        {
            if (group.Key < 0 || group.Key >= paragraphs.Count)
            {
                continue;
            }

            var paragraph = paragraphs[group.Key];
            var fullText = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;

            var ordered = group
                .Where(h => h.StartOffset >= 0 && h.Length > 0 && h.StartOffset + h.Length <= fullText.Length)
                .OrderBy(h => h.StartOffset)
                .ToList();
            if (ordered.Count == 0)
            {
                continue;
            }

            paragraph.Inlines.Clear();
            var cursor = 0;
            foreach (var highlight in ordered)
            {
                if (highlight.StartOffset < cursor)
                {
                    continue; // 겹치는 구간은 지원하지 않는다 - 먼저 온 하이라이트를 우선한다.
                }

                if (highlight.StartOffset > cursor)
                {
                    paragraph.Inlines.Add(new Run(fullText[cursor..highlight.StartOffset]));
                }

                var highlightRun = new Run(fullText.Substring(highlight.StartOffset, highlight.Length)) { Background = Brushes.Yellow };
                if (!string.IsNullOrEmpty(highlight.Note))
                {
                    highlightRun.ToolTip = highlight.Note;
                }

                paragraph.Inlines.Add(highlightRun);
                cursor = highlight.StartOffset + highlight.Length;
            }

            if (cursor < fullText.Length)
            {
                paragraph.Inlines.Add(new Run(fullText[cursor..]));
            }
        }
    }

    partial void OnSearchQueryChanged(string value) => _lastSearchParagraphIndex = -1;

    partial void OnCurrentPageNumberChanged(int value)
    {
        OnPropertyChanged(nameof(ReadingProgressText));
        _positionSaveTimer.Stop();
        _positionSaveTimer.Start();
    }

    partial void OnPageCountChanged(int value) => OnPropertyChanged(nameof(ReadingProgressText));

    partial void OnFontFamilyNameChanged(string value) => OnFormattingChanged();

    partial void OnFontSizeChanged(double value) => OnFormattingChanged();

    partial void OnLineSpacingMultiplierChanged(double value) => OnFormattingChanged();

    partial void OnMarginPresetChanged(MarginPreset value) => OnFormattingChanged();

    partial void OnThemeChanged(ReadingTheme value)
    {
        OnPropertyChanged(nameof(PageBackgroundBrush));
        OnFormattingChanged();
    }

    partial void OnDimmingOpacityChanged(double value) => SaveDisplaySettings();

    private void OnFormattingChanged()
    {
        ApplyDocumentFormatting();
        SaveDisplaySettings();
    }

    private void SaveDisplaySettings()
    {
        // 창 크기/위치 등 이 ViewModel이 모르는 다른 설정 필드를 덮어쓰지 않도록 읽고-수정하고-저장한다.
        var settings = _settingsService.Load();
        settings.FontFamilyName = FontFamilyName;
        settings.FontSize = FontSize;
        settings.LineSpacingMultiplier = LineSpacingMultiplier;
        settings.MarginPreset = MarginPreset;
        settings.Theme = Theme;
        settings.DimmingOpacity = DimmingOpacity;
        _settingsService.Save(settings);
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
        Document.Background = GetPageBackgroundBrush();
        Document.Foreground = GetForegroundBrush();

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

    private Brush GetPageBackgroundBrush() => Theme switch
    {
        ReadingTheme.Dark => new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
        ReadingTheme.Sepia => new SolidColorBrush(Color.FromRgb(0xF4, 0xEC, 0xD8)),
        _ => CreatePaperTextureBrush(),
    };

    private Brush GetForegroundBrush() => Theme switch
    {
        ReadingTheme.Dark => new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)),
        ReadingTheme.Sepia => new SolidColorBrush(Color.FromRgb(0x5B, 0x46, 0x36)),
        _ => new SolidColorBrush(Color.FromRgb(0x2B, 0x26, 0x20)),
    };

    private static Brush CreatePaperTextureBrush()
    {
        var image = new BitmapImage(new Uri("pack://application:,,,/Assets/Textures/paper.png"));
        return new ImageBrush(image)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, image.PixelWidth, image.PixelHeight),
            ViewportUnits = BrushMappingMode.Absolute,
        };
    }

    private static readonly Regex ChapterHeadingRegex = new(@"^(제\s*\d+\s*장|Chapter\s+\d+)", RegexOptions.IgnoreCase);

    private static (FlowDocument Document, List<TocEntry> TocEntries) BuildFlowDocument(string content)
    {
        var document = new FlowDocument();
        var tocEntries = new List<TocEntry>();

        var paragraphBlocks = Regex.Split(content, @"\r?\n\s*\r?\n");
        foreach (var block in paragraphBlocks)
        {
            var normalized = Regex.Replace(block, @"\r?\n", " ").Trim();
            if (normalized.Length == 0)
            {
                continue;
            }

            var paragraph = new Paragraph(new Run(normalized));
            document.Blocks.Add(paragraph);

            if (ChapterHeadingRegex.IsMatch(normalized))
            {
                var title = normalized.Length > 40 ? normalized[..40] + "…" : normalized;
                tocEntries.Add(new TocEntry(title, paragraph));
            }
        }

        return (document, tocEntries);
    }
}
