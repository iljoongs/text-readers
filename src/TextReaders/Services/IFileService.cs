using TextReaders.Models;

namespace TextReaders.Services;

public interface IFileService
{
    Book? OpenBookFromDialog();

    Book LoadBook(string filePath);
}
