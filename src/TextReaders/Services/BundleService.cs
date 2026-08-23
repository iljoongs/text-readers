using Microsoft.Win32;
using TextReaders.Models;

namespace TextReaders.Services;

public sealed class BundleService : IBundleService
{
    public string? ShowSaveFileDialog(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "text-readers 번들 (*.json)|*.json",
            DefaultExt = "json",
            FileName = suggestedFileName,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public ReaderBundle Load(string filePath) =>
        JsonFileStore.Load(filePath, () => new ReaderBundle { Content = string.Empty });

    public void Save(string filePath, ReaderBundle bundle) => JsonFileStore.Save(filePath, bundle);
}
