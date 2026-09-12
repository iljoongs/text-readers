using TextReaders.Models;

namespace TextReaders.Services;

public interface IBookStorageService
{
    string SaveBook(string content, string title, string author);

    string LoadBook(string filePathOrHash);

    void DeleteBook(string filePathOrHash);

    IReadOnlyDictionary<string, BookIndexEntry> GetIndex();
}
