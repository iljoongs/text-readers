using System.IO;
using System.Windows;
using TextReaders.Models;
using TextReaders.Services;
using TextReaders.ViewModels;

namespace TextReaders;

public partial class App : Application
{
    private const double MinRestorableSize = 200;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        IEncodingDetectionService encodingDetectionService = new EncodingDetectionService();
        IFileService fileService = new FileService(encodingDetectionService);
        ISettingsService settingsService = new SettingsService();
        ILibraryService libraryService = new LibraryService();
        IFontService fontService = new FontService();
        ITextToSpeechService ttsService = new TextToSpeechService();
        IBundleService bundleService = new BundleService();
        IBookStorageService bookStorageService = new BookStorageService();

        var readerViewModel = new ReaderViewModel(fileService, settingsService, libraryService, fontService, ttsService, bundleService, bookStorageService);

        var lastOpenedFilePath = libraryService.GetLastOpenedFilePath();
        if (lastOpenedFilePath is not null && File.Exists(lastOpenedFilePath))
        {
            try
            {
                readerViewModel.OpenPath(lastOpenedFilePath);
            }
            catch (IOException)
            {
                // 마지막으로 연 파일을 더 이상 읽을 수 없어도 앱 시작 자체는 계속 진행한다.
            }
        }

        var mainWindow = new MainWindow
        {
            DataContext = readerViewModel,
        };

        ApplyWindowGeometry(mainWindow, settingsService.Load().Window);
        mainWindow.Closing += (_, _) => SaveWindowGeometry(mainWindow, settingsService);

        mainWindow.Show();
    }

    private static void ApplyWindowGeometry(Window window, WindowGeometry? geometry)
    {
        if (geometry is null || geometry.Width < MinRestorableSize || geometry.Height < MinRestorableSize)
        {
            return;
        }

        window.Left = geometry.Left;
        window.Top = geometry.Top;
        window.Width = geometry.Width;
        window.Height = geometry.Height;
        window.WindowStartupLocation = WindowStartupLocation.Manual;

        if (geometry.IsMaximized)
        {
            window.WindowState = WindowState.Maximized;
        }
    }

    private static void SaveWindowGeometry(Window window, ISettingsService settingsService)
    {
        var isMaximized = window.WindowState == WindowState.Maximized;
        var bounds = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;

        // 창 크기/위치 외 다른 설정 필드를 덮어쓰지 않도록 읽고-수정하고-저장한다.
        var settings = settingsService.Load();
        settings.Window = new WindowGeometry
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            IsMaximized = isMaximized,
        };
        settingsService.Save(settings);
    }
}

