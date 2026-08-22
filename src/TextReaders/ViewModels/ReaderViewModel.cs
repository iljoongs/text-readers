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
    private string _fontFamilyName;

    [ObservableProperty]
    private double _fontSize;

    [ObservableProperty]
    private double _lineSpacingMultiplier;

    [ObservableProperty]
    private MarginPreset _marginPreset;

    [ObservableProperty]
    private ReadingTheme _theme;

    public event Action<int>? NavigateToPageRequested;

    public IReadOnlyList<string> AvailableFontFamilyNames => FontCatalog.AvailableFontFamilyNames;

    public IReadOnlyList<MarginPreset> MarginPresetOptions { get; } = Enum.GetValues<MarginPreset>();

    public IReadOnlyList<ReadingTheme> ThemeOptions { get; } = Enum.GetValues<ReadingTheme>();

    public Brush PageBackgroundBrush => GetPageBackgroundBrush();

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
        Document = BuildFlowDocument(book.Content);
        ApplyDocumentFormatting();

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

    partial void OnCurrentPageNumberChanged(int value)
    {
        _positionSaveTimer.Stop();
        _positionSaveTimer.Start();
    }

    partial void OnFontFamilyNameChanged(string value) => OnFormattingChanged();

    partial void OnFontSizeChanged(double value) => OnFormattingChanged();

    partial void OnLineSpacingMultiplierChanged(double value) => OnFormattingChanged();

    partial void OnMarginPresetChanged(MarginPreset value) => OnFormattingChanged();

    partial void OnThemeChanged(ReadingTheme value)
    {
        OnPropertyChanged(nameof(PageBackgroundBrush));
        OnFormattingChanged();
    }

    private void OnFormattingChanged()
    {
        ApplyDocumentFormatting();

        // 창 크기/위치 등 이 ViewModel이 모르는 다른 설정 필드를 덮어쓰지 않도록 읽고-수정하고-저장한다.
        var settings = _settingsService.Load();
        settings.FontFamilyName = FontFamilyName;
        settings.FontSize = FontSize;
        settings.LineSpacingMultiplier = LineSpacingMultiplier;
        settings.MarginPreset = MarginPreset;
        settings.Theme = Theme;
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
