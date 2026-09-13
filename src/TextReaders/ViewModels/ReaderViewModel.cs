using System.Collections.ObjectModel;
using System.IO;
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
    private readonly IFontService _fontService;
    private readonly ITextToSpeechService _ttsService;
    private readonly IBundleService _bundleService;
    private readonly IBookStorageService _bookStorageService;
    private readonly DispatcherTimer _positionSaveTimer;
    private readonly DispatcherTimer _readingTimeTimer;
    private static readonly TimeSpan ReadingTimeTickInterval = TimeSpan.FromSeconds(30);

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

    [ObservableProperty]
    private bool _isSpeaking;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private LibraryEntry? _selectedLibraryEntry;

    private int _lastSearchParagraphIndex = -1;

    // Menu > File > 저장이 덮어쓸 대상. 번들(.json)로 열거나 저장한 적이 없으면 null이고,
    // 이 경우 저장은 다른 이름으로 저장과 동일하게 동작한다.
    private string? _currentBundlePath;

    public event Action<int>? NavigateToPageRequested;

    public event Action<Paragraph>? NavigateToParagraphRequested;

    public IReadOnlyList<string> AvailableFontFamilyNames => _fontService.GetAvailableFontFamilyNames();

    public string DataDirectoryPath => AppPaths.DataDirectory;

    public bool IsDefaultDataDirectory => AppPaths.IsDefaultDataDirectory;

    public IReadOnlyList<MarginPreset> MarginPresetOptions { get; } = Enum.GetValues<MarginPreset>();

    public IReadOnlyList<ReadingTheme> ThemeOptions { get; } = Enum.GetValues<ReadingTheme>();

    public Brush PageBackgroundBrush => GetPageBackgroundBrush();

    public string ReadingProgressText => PageCount > 0
        ? $"{CurrentPageNumber} / {PageCount} ({(int)Math.Round(CurrentPageNumber * 100.0 / PageCount)}%)"
        : string.Empty;

    public ObservableCollection<Bookmark> Bookmarks { get; } = new();

    public ObservableCollection<TocEntry> TableOfContents { get; } = new();

    public ObservableCollection<LibraryEntry> LibraryEntries { get; } = new();

    public string TotalReadingTimeText
    {
        get
        {
            var totalMinutes = (int)(LibraryEntries.Sum(e => e.TotalReadingSeconds) / 60);
            return totalMinutes >= 60 ? $"{totalMinutes / 60}시간 {totalMinutes % 60}분" : $"{totalMinutes}분";
        }
    }

    public int CompletedBookCount => LibraryEntries.Count(e => e.IsCompleted);

    public bool CanStartReading => !IsSpeaking;

    public ReaderViewModel(IFileService fileService, ISettingsService settingsService, ILibraryService libraryService, IFontService fontService, ITextToSpeechService ttsService, IBundleService bundleService, IBookStorageService bookStorageService)
    {
        _fileService = fileService;
        _settingsService = settingsService;
        _libraryService = libraryService;
        _fontService = fontService;
        _ttsService = ttsService;
        _bundleService = bundleService;
        _bookStorageService = bookStorageService;

        // TTS 이벤트는 SpeechSynthesizer의 백그라운드 스레드에서 발생하므로 UI 스레드로 넘겨준다.
        _ttsService.ParagraphStarted += paragraph =>
            Application.Current.Dispatcher.BeginInvoke(() => NavigateToParagraphRequested?.Invoke(paragraph));
        _ttsService.PlaybackStopped += () =>
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                IsSpeaking = false;
                IsPaused = false;
            });

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

        // 책이 열려 있고 창이 활성 상태일 때만 독서 시간을 누적한다.
        _readingTimeTimer = new DispatcherTimer { Interval = ReadingTimeTickInterval };
        _readingTimeTimer.Tick += (_, _) =>
        {
            if (CurrentBook is not null && (Application.Current.MainWindow?.IsActive ?? false))
            {
                _libraryService.AddReadingTime(CurrentBook.FilePath, ReadingTimeTickInterval.TotalSeconds);
                RefreshLibraryEntries();
            }
        };
        _readingTimeTimer.Start();

        RefreshLibraryEntries();
    }

    [RelayCommand]
    private void OpenFile()
    {
        try
        {
            var path = _fileService.ShowOpenFileDialog();
            if (path is not null)
            {
                OpenPath(path);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"파일을 여는 중 오류가 발생했습니다.\n{ex.Message}", "text-readers",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // 확장자가 .json이면 번들, .mybook이면 표준 ZIP 책 파일, 그 외에는 일반 txt/md로 취급한다.
    // OpenFile/라이브러리 항목 열기/마지막 세션 복원 세 경로가 모두 이 메서드를 거친다.
    public void OpenPath(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            OpenBundle(filePath);
        }
        else if (extension.Equals(".mybook", StringComparison.OrdinalIgnoreCase))
        {
            OpenMyBookPath(filePath);
        }
        else
        {
            var book = _fileService.LoadBook(filePath);
            // 파일명이 아니라 본문의 "제목: ..." 줄에서 인식된 제목이면 라이브러리 카드에도 반영한다.
            // 마커가 없으면 null이 되어 이전에 저장된 표시 제목도 함께 지워져 파일명으로 되돌아간다.
            _libraryService.SetDisplayTitle(filePath, TitleDetector.DetectTitle(book.Content));
            LoadBook(book);
        }
    }

    private static bool IsMyBookPath(string filePath) =>
        Path.GetExtension(filePath).Equals(".mybook", StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private void OpenMyBook()
    {
        try
        {
            var path = _bookStorageService.ShowOpenFileDialog();
            if (path is not null)
            {
                OpenPath(path);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"mybook 파일을 여는 중 오류가 발생했습니다.\n{ex.Message}", "text-readers",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // 라이브러리 항목의 제목은 파일명에서 못 뽑으므로(파일명이 해시), 인덱스에서 실제 제목을 찾아
    // DisplayTitle로 심어둔다 - 열 때마다 갱신되므로 다른 곳에서 저장된 mybook도 열면 제목이 맞춰진다.
    private void OpenMyBookPath(string filePath)
    {
        var content = _bookStorageService.LoadBook(filePath);
        var hash = Path.GetFileNameWithoutExtension(filePath);
        var title = _bookStorageService.GetIndex().TryGetValue(hash, out var indexEntry)
            ? indexEntry.Title
            : hash;

        _libraryService.SetDisplayTitle(filePath, title);

        LoadBook(new Book
        {
            FilePath = filePath,
            Title = title,
            Content = content,
            IsMarkdown = false,
        });
    }

    // 현재 책의 내용을 새 .mybook 파일로 저장한다(비파괴적: 원본 항목은 그대로 두고 별도 항목을 추가).
    public void SaveCurrentAsMyBook(string title, string author)
    {
        if (CurrentBook is null)
        {
            return;
        }

        var highlights = _libraryService.GetHighlights(CurrentBook.FilePath).ToList();
        SaveAsMyBookAndTrack(CurrentBook.Content, title, author, Bookmarks.ToList(), highlights, CurrentPageNumber);
    }

    [RelayCommand]
    private void SaveAsMyBook()
    {
        if (CurrentBook is null)
        {
            MessageBox.Show("먼저 책을 열어주세요.", "text-readers", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            SaveCurrentAsMyBook(CurrentBook.Title, string.Empty);
            MessageBox.Show("mybook으로 저장했습니다.", "text-readers", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"mybook으로 저장하는 중 오류가 발생했습니다.\n{ex.Message}", "text-readers",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void SaveLibraryEntryAsMyBook(LibraryEntry entry)
    {
        try
        {
            if (entry.IsMyBook)
            {
                MessageBox.Show("이미 mybook 형식입니다.", "text-readers", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var content = ReadEntryContent(entry);
            SaveAsMyBookAndTrack(content, entry.Title, string.Empty, entry.Bookmarks, entry.Highlights, entry.LastPageIndex);

            MessageBox.Show("mybook으로 저장했습니다.", "text-readers", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"mybook으로 저장하는 중 오류가 발생했습니다.\n{ex.Message}", "text-readers",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // 기존 항목은 그대로 두고, 같은 내용을 새 .mybook 파일로도 저장해 라이브러리에 별도 항목으로 추가한다.
    private void SaveAsMyBookAndTrack(string content, string title, string author,
        IReadOnlyList<Bookmark> bookmarks, IReadOnlyList<Highlight> highlights, int lastPageIndex)
    {
        var savedPath = _bookStorageService.SaveBook(content, title, author);

        _libraryService.ImportEntry(savedPath, bookmarks, highlights, lastPageIndex);
        _libraryService.SetDisplayTitle(savedPath, title);

        RefreshLibraryEntries();
    }

    [RelayCommand]
    private void ConvertLibraryEntryToMyBook(LibraryEntry entry)
    {
        try
        {
            if (entry.IsMyBook)
            {
                MessageBox.Show("이미 mybook 형식입니다.", "text-readers", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var oldFilePath = entry.FilePath;
            var content = ReadEntryContent(entry);
            var savedPath = _bookStorageService.SaveBook(content, entry.Title, string.Empty);

            // 원본 파일은 그대로 두고(요구사항), 같은 라이브러리 항목이 새 mybook 파일을 가리키도록
            // FilePath만 옮긴다 - 북마크/하이라이트/마지막 페이지는 같은 레코드에 실려 있어 그대로 유지된다.
            _libraryService.RenameEntry(oldFilePath, savedPath);
            _libraryService.SetDisplayTitle(savedPath, entry.Title);

            if (CurrentBook?.FilePath == oldFilePath)
            {
                OpenMyBookPath(savedPath);
            }

            RefreshLibraryEntries();
            MessageBox.Show("mybook으로 변경했습니다. (원본 파일은 그대로 유지됩니다)", "text-readers",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"mybook으로 변경하는 중 오류가 발생했습니다.\n{ex.Message}", "text-readers",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // 라이브러리 목록에서만 제거한다 - 실제 파일은 지우지 않는다.
    [RelayCommand]
    private void RemoveLibraryEntry(LibraryEntry entry)
    {
        _libraryService.RemoveEntry(entry.FilePath);
        if (SelectedLibraryEntry?.FilePath == entry.FilePath)
        {
            SelectedLibraryEntry = null;
        }

        RefreshLibraryEntries();
    }

    // 라이브러리 정보(파일명/제목/저자/추가일/해시/크기/경로)를 사람이 읽을 텍스트로 만든다.
    // mybook이 아니면 그 사실만 안내한다.
    public string GetMyBookInfoText(LibraryEntry entry)
    {
        if (!entry.IsMyBook)
        {
            return $"mybook 형식이 아닙니다.\n\n파일: {Path.GetFileName(entry.FilePath)}";
        }

        var hash = Path.GetFileNameWithoutExtension(entry.FilePath);
        var indexEntry = _bookStorageService.GetIndex().TryGetValue(hash, out var found) ? found : null;
        var fileSize = File.Exists(entry.FilePath) ? new FileInfo(entry.FilePath).Length : 0;

        var author = string.IsNullOrWhiteSpace(indexEntry?.Author) ? "(없음)" : indexEntry.Author;
        var addedAt = indexEntry is not null ? indexEntry.AddedAt.ToString("yyyy-MM-dd HH:mm") : "알 수 없음";

        return string.Join('\n',
            $"파일명: {Path.GetFileName(entry.FilePath)}",
            $"제목: {entry.Title}",
            $"저자: {author}",
            $"추가된 날짜: {addedAt}",
            $"해시(SHA256 앞 32자): {hash}",
            $"파일 크기: {FormatFileSize(fileSize)}",
            $"경로: {entry.FilePath}");
    }

    private static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / 1024.0 / 1024.0:0.#} MB",
    };

    // 드래그 앤 드롭으로 라이브러리에 파일을 추가한다 - 지금 읽고 있는 책은 바꾸지 않고 목록에만 등록한다.
    // 지원하지 않는 확장자면 false를 반환한다(호출부에서 결과를 모아 안내 메시지를 보여줌).
    public bool AddFileToLibrary(string filePath)
    {
        try
        {
            var extension = Path.GetExtension(filePath);
            if (extension.Equals(".mybook", StringComparison.OrdinalIgnoreCase))
            {
                var hash = Path.GetFileNameWithoutExtension(filePath);
                var title = _bookStorageService.GetIndex().TryGetValue(hash, out var indexEntry) ? indexEntry.Title : hash;
                _libraryService.SetDisplayTitle(filePath, title);
            }
            else if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) || extension.Equals(".md", StringComparison.OrdinalIgnoreCase))
            {
                var book = _fileService.LoadBook(filePath);
                _libraryService.SetDisplayTitle(filePath, TitleDetector.DetectTitle(book.Content));
            }
            else
            {
                return false;
            }

            _libraryService.EnsureEntryExists(filePath);
            RefreshLibraryEntries();
            return true;
        }
        catch
        {
            return false;
        }
    }

    // 라이브러리 창 자체의 크기/위치와 마지막으로 선택했던 항목을 기억해 다음에 열 때 복구한다.
    public WindowGeometry? LibraryWindowGeometry => _settingsService.Load().LibraryWindow;

    public string? LastSelectedLibraryEntryPath => _settingsService.Load().SelectedLibraryEntryPath;

    public void SaveLibraryWindowState(WindowGeometry? geometry, string? selectedEntryPath)
    {
        var settings = _settingsService.Load();
        settings.LibraryWindow = geometry;
        settings.SelectedLibraryEntryPath = selectedEntryPath;
        _settingsService.Save(settings);
    }

    // mybook/번들이 아닌 라이브러리 항목(txt/md)의 원문을 읽어온다.
    private string ReadEntryContent(LibraryEntry entry)
    {
        if (Path.GetExtension(entry.FilePath).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return _bundleService.Load(entry.FilePath).Content;
        }

        return _fileService.LoadBook(entry.FilePath).Content;
    }

    private void OpenBundle(string filePath)
    {
        var bundle = _bundleService.Load(filePath);

        _libraryService.ImportEntry(filePath, bundle.Bookmarks, bundle.Highlights, bundle.LastPageIndex);

        FontFamilyName = bundle.FontFamilyName;
        FontSize = bundle.FontSize;
        LineSpacingMultiplier = bundle.LineSpacingMultiplier;
        MarginPreset = bundle.MarginPreset;
        Theme = bundle.Theme;
        DimmingOpacity = bundle.DimmingOpacity;

        var detectedTitle = TitleDetector.DetectTitle(bundle.Content);
        _libraryService.SetDisplayTitle(filePath, detectedTitle);

        // LoadBook이 시작하면서 _currentBundlePath를 null로 초기화하므로, 그 뒤에 다시 설정한다.
        LoadBook(new Book
        {
            FilePath = filePath,
            Title = detectedTitle ?? Path.GetFileNameWithoutExtension(filePath),
            Content = bundle.Content,
            IsMarkdown = bundle.IsMarkdown,
        });
        _currentBundlePath = filePath;
    }

    [RelayCommand]
    private void Save()
    {
        if (CurrentBook is null)
        {
            return;
        }

        if (_currentBundlePath is not null)
        {
            SaveBundleTo(_currentBundlePath);
        }
        else
        {
            SaveAs();
        }
    }

    [RelayCommand]
    private void SaveAs()
    {
        if (CurrentBook is null)
        {
            return;
        }

        var suggestedFileName = Path.GetFileNameWithoutExtension(CurrentBook.FilePath) + ".json";
        var path = _bundleService.ShowSaveFileDialog(suggestedFileName);
        if (path is not null)
        {
            SaveBundleTo(path);
        }
    }

    private void SaveBundleTo(string filePath)
    {
        if (CurrentBook is null)
        {
            return;
        }

        var bundle = new ReaderBundle
        {
            Content = CurrentBook.Content,
            IsMarkdown = CurrentBook.IsMarkdown,
            FontFamilyName = FontFamilyName,
            FontSize = FontSize,
            LineSpacingMultiplier = LineSpacingMultiplier,
            MarginPreset = MarginPreset,
            Theme = Theme,
            DimmingOpacity = DimmingOpacity,
            LastPageIndex = CurrentPageNumber,
            Bookmarks = Bookmarks.ToList(),
            Highlights = _libraryService.GetHighlights(CurrentBook.FilePath).ToList(),
        };

        _bundleService.Save(filePath, bundle);
        _currentBundlePath = filePath;
    }

    public void LoadBook(Book book)
    {
        // 새 문서를 불러오면 이전 문서의 Paragraph를 가리키던 TTS 재생을 이어갈 수 없으므로 멈춘다.
        StopReading();

        // 번들이 아닌 새 파일을 열면(txt/md/라이브러리 항목) "저장"이 번들을 덮어쓰지 않도록 초기화한다.
        // OpenBundle은 이 값을 이 호출 이후에 다시 설정한다.
        _currentBundlePath = null;

        CurrentBook = book;
        RebuildDocumentFromCurrentBook();

        Bookmarks.Clear();
        foreach (var bookmark in _libraryService.GetBookmarks(book.FilePath))
        {
            Bookmarks.Add(bookmark);
        }

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

        RefreshLibraryEntries();
    }

    // LoadBook과 UpdateContent가 공유하는 "CurrentBook.Content로 Document/목차/하이라이트를 다시 그리기".
    // 북마크/읽기 위치/라이브러리 갱신처럼 "새 책을 여는" 맥락에서만 필요한 절차는 포함하지 않는다.
    private void RebuildDocumentFromCurrentBook()
    {
        if (CurrentBook is null)
        {
            return;
        }

        var (document, tocEntries) = CurrentBook.IsMarkdown
            ? MarkdownFlowDocumentBuilder.Build(CurrentBook.Content)
            : BuildFlowDocument(CurrentBook.Content);
        Document = document;
        ApplyDocumentFormatting();

        TableOfContents.Clear();
        foreach (var entry in tocEntries)
        {
            TableOfContents.Add(entry);
        }

        ApplyHighlights(_libraryService.GetHighlights(CurrentBook.FilePath));
    }

    // Text > Edit. 같은 책의 본문만 바꾸는 것이므로 LoadBook 전체를 다시 타지 않고
    // 문서만 재구성한다 - 북마크/하이라이트의 문단 오프셋은 편집 후 어긋날 수 있음을 감수한다
    // (폰트를 바꾸면 페이지 번호 기반 북마크가 밀리는 것과 같은 성격의 단순화).
    public void UpdateContent(string newContent)
    {
        if (CurrentBook is null)
        {
            return;
        }

        CurrentBook = new Book
        {
            FilePath = CurrentBook.FilePath,
            Title = CurrentBook.Title,
            Content = newContent,
            IsMarkdown = CurrentBook.IsMarkdown,
        };

        RebuildDocumentFromCurrentBook();

        if (_currentBundlePath is not null)
        {
            SaveBundleTo(_currentBundlePath);
        }
        else if (IsMyBookPath(CurrentBook.FilePath))
        {
            SaveMyBookContentInPlace();
        }
        else
        {
            File.WriteAllText(CurrentBook.FilePath, newContent);
        }
    }

    // mybook은 파일명이 내용의 해시라 "제자리 덮어쓰기"가 불가능하다 - 내용이 바뀌면 새 해시 파일이
    // 생기므로, 그 파일로 라이브러리 항목을 옮기고(북마크/하이라이트/위치는 그대로 유지) CurrentBook도
    // 새 경로를 가리키게 한 뒤 이전 해시 파일은 삭제한다(편집할 때마다 옛 버전이 쌓이지 않도록).
    private void SaveMyBookContentInPlace()
    {
        var oldFilePath = CurrentBook!.FilePath;
        var savedPath = _bookStorageService.SaveBook(CurrentBook.Content, CurrentBook.Title, string.Empty);

        if (string.Equals(savedPath, oldFilePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _libraryService.RenameEntry(oldFilePath, savedPath);
        _libraryService.SetDisplayTitle(savedPath, CurrentBook.Title);
        _bookStorageService.DeleteBook(oldFilePath);

        CurrentBook = new Book
        {
            FilePath = savedPath,
            Title = CurrentBook.Title,
            Content = CurrentBook.Content,
            IsMarkdown = CurrentBook.IsMarkdown,
        };
    }

    // Text > Edit Title. 제목은 파일 이름에서 그대로 가져오는 값이라, 제목을 바꾸는 것은
    // 실제로 파일 이름을 바꾸는 것과 같다. 라이브러리 항목/번들 경로도 새 경로로 옮겨준다.
    public void RenameCurrentFile(string newTitle)
    {
        if (CurrentBook is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(newTitle))
        {
            throw new ArgumentException("제목을 입력해주세요.");
        }

        // mybook은 파일명이 내용의 해시라 제목과 무관하다 - 실제 파일은 그대로 두고 표시용 제목만 바꾼다.
        if (IsMyBookPath(CurrentBook.FilePath))
        {
            _libraryService.SetDisplayTitle(CurrentBook.FilePath, newTitle);
            CurrentBook = new Book
            {
                FilePath = CurrentBook.FilePath,
                Title = newTitle,
                Content = CurrentBook.Content,
                IsMarkdown = CurrentBook.IsMarkdown,
            };
            RefreshLibraryEntries();
            return;
        }

        if (newTitle.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("파일 이름에 사용할 수 없는 문자가 포함되어 있습니다.");
        }

        var directory = Path.GetDirectoryName(CurrentBook.FilePath)!;
        var extension = Path.GetExtension(CurrentBook.FilePath);
        var newFilePath = Path.Combine(directory, newTitle + extension);

        if (string.Equals(newFilePath, CurrentBook.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (File.Exists(newFilePath))
        {
            throw new IOException("같은 이름의 파일이 이미 있습니다.");
        }

        File.Move(CurrentBook.FilePath, newFilePath);
        _libraryService.RenameEntry(CurrentBook.FilePath, newFilePath);

        if (_currentBundlePath == CurrentBook.FilePath)
        {
            _currentBundlePath = newFilePath;
        }

        CurrentBook = new Book
        {
            FilePath = newFilePath,
            Title = newTitle,
            Content = CurrentBook.Content,
            IsMarkdown = CurrentBook.IsMarkdown,
        };

        RefreshLibraryEntries();
    }

    [RelayCommand]
    private void OpenLibraryEntry(LibraryEntry entry)
    {
        try
        {
            OpenPath(entry.FilePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"파일을 여는 중 오류가 발생했습니다.\n{ex.Message}", "text-readers",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshLibraryEntries()
    {
        LibraryEntries.Clear();
        foreach (var entry in _libraryService.GetAllEntries())
        {
            LibraryEntries.Add(entry);
        }

        OnPropertyChanged(nameof(TotalReadingTimeText));
        OnPropertyChanged(nameof(CompletedBookCount));
    }

    // 폰트/여백을 바꾸면 PageCount가 바뀔 수 있어 두 프로퍼티 변경 시 모두 확인한다.
    private void CheckCompletion()
    {
        if (CurrentBook is not null && PageCount > 1 && CurrentPageNumber == PageCount)
        {
            _libraryService.MarkCompleted(CurrentBook.FilePath);
            RefreshLibraryEntries();
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

    [RelayCommand]
    private void AddCustomFont()
    {
        try
        {
            var familyName = _fontService.AddCustomFontFromDialog();
            if (familyName is null)
            {
                return;
            }

            OnPropertyChanged(nameof(AvailableFontFamilyNames));
            FontFamilyName = familyName;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"폰트를 추가하는 중 오류가 발생했습니다.\n{ex.Message}", "text-readers",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void BrowseDataDirectory()
    {
        var chosen = AppPaths.ChooseDataDirectoryFromDialog();
        if (chosen is not null)
        {
            ChangeDataDirectory(chosen);
        }
    }

    [RelayCommand]
    private void ResetDataDirectory() => ChangeDataDirectory(null);

    [RelayCommand]
    private void CopyDefaultData()
    {
        if (IsDefaultDataDirectory)
        {
            MessageBox.Show("이미 기본 폴더를 사용 중입니다.", "text-readers", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            AppPaths.CopyDefaultDataToCurrentDirectory();
            _fontService.RescanCustomFonts();
            OnPropertyChanged(nameof(AvailableFontFamilyNames));

            MessageBox.Show("기본 폴더의 데이터를 현재 데이터 폴더로 복사했습니다.", "text-readers", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"기본 폴더 데이터를 복사하는 중 오류가 발생했습니다.\n{ex.Message}", "text-readers",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ChangeDataDirectory(string? newDirectory)
    {
        try
        {
            AppPaths.ChangeDataDirectory(newDirectory);
            _fontService.RescanCustomFonts();

            OnPropertyChanged(nameof(DataDirectoryPath));
            OnPropertyChanged(nameof(IsDefaultDataDirectory));
            OnPropertyChanged(nameof(AvailableFontFamilyNames));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"데이터 폴더를 변경하는 중 오류가 발생했습니다.\n{ex.Message}", "text-readers",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // 시작 문단은 현재 페이지의 페이지네이터를 가진 View 쪽(MainWindow)에서 GetPageNumber로 찾아 넘겨준다.
    public void StartReading(IReadOnlyList<Paragraph> paragraphs, int startIndex)
    {
        if (paragraphs.Count == 0)
        {
            return;
        }

        _ttsService.Play(paragraphs, Math.Clamp(startIndex, 0, paragraphs.Count - 1));
        IsSpeaking = true;
        IsPaused = false;
    }

    [RelayCommand]
    private void TogglePause()
    {
        if (!IsSpeaking)
        {
            return;
        }

        if (IsPaused)
        {
            _ttsService.Resume();
            IsPaused = false;
        }
        else
        {
            _ttsService.Pause();
            IsPaused = true;
        }
    }

    [RelayCommand]
    private void StopReading()
    {
        _ttsService.Stop();
        IsSpeaking = false;
        IsPaused = false;
    }

    partial void OnIsSpeakingChanged(bool value) => OnPropertyChanged(nameof(CanStartReading));

    partial void OnSearchQueryChanged(string value) => _lastSearchParagraphIndex = -1;

    partial void OnCurrentPageNumberChanged(int value)
    {
        OnPropertyChanged(nameof(ReadingProgressText));
        _positionSaveTimer.Stop();
        _positionSaveTimer.Start();
        CheckCompletion();
    }

    partial void OnPageCountChanged(int value)
    {
        OnPropertyChanged(nameof(ReadingProgressText));
        CheckCompletion();
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

        Document.FontFamily = _fontService.ResolveFontFamily(FontFamilyName);
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
