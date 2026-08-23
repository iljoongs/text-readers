using TextReaders.Models;

namespace TextReaders.Services;

public interface IBundleService
{
    string? ShowSaveFileDialog(string suggestedFileName);

    ReaderBundle Load(string filePath);

    void Save(string filePath, ReaderBundle bundle);
}
