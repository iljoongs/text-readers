using TextReaders.Models;

namespace TextReaders.Services;

public interface IFileService
{
    string? ShowOpenFileDialog();

    Book LoadBook(string filePath);
}
