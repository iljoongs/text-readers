using System.IO;
using System.Windows;
using TextReaders.Services;
using TextReaders.ViewModels;

namespace TextReaders;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        IEncodingDetectionService encodingDetectionService = new EncodingDetectionService();
        IFileService fileService = new FileService(encodingDetectionService);
        ISettingsService settingsService = new SettingsService();
        ILibraryService libraryService = new LibraryService();

        var readerViewModel = new ReaderViewModel(fileService, settingsService, libraryService);

        var lastOpenedFilePath = libraryService.GetLastOpenedFilePath();
        if (lastOpenedFilePath is not null && File.Exists(lastOpenedFilePath))
        {
            try
            {
                readerViewModel.LoadBook(fileService.LoadBook(lastOpenedFilePath));
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
        mainWindow.Show();
    }
}

