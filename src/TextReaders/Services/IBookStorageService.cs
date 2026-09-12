using TextReaders.Models;

namespace TextReaders.Services;

public interface IBookStorageService
{
    // .mybook 파일 선택 다이얼로그를 띄운다. 사용자가 취소하면 null.
    string? ShowOpenFileDialog();

    string SaveBook(string content, string title, string author);

    string LoadBook(string filePathOrHash);

    void DeleteBook(string filePathOrHash);

    IReadOnlyDictionary<string, BookIndexEntry> GetIndex();
}
