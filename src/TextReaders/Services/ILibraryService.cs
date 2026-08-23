using TextReaders.Models;

namespace TextReaders.Services;

public interface ILibraryService
{
    string? GetLastOpenedFilePath();

    IReadOnlyList<LibraryEntry> GetAllEntries();

    int GetLastPageIndex(string filePath);

    void UpdatePosition(string filePath, int pageIndex);

    IReadOnlyList<Bookmark> GetBookmarks(string filePath);

    void AddBookmark(string filePath, Bookmark bookmark);

    void RemoveBookmark(string filePath, Bookmark bookmark);

    IReadOnlyList<Highlight> GetHighlights(string filePath);

    void AddHighlight(string filePath, Highlight highlight);
}
