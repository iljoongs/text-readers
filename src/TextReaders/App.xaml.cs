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

        var readerViewModel = new ReaderViewModel(fileService);

        var mainWindow = new MainWindow
        {
            DataContext = readerViewModel,
        };
        mainWindow.Show();
    }
}

