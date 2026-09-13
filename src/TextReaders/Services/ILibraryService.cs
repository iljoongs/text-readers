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

    void AddReadingTime(string filePath, double seconds);

    void MarkCompleted(string filePath);

    void ImportEntry(string filePath, IReadOnlyList<Bookmark> bookmarks, IReadOnlyList<Highlight> highlights, int lastPageIndex);

    void RenameEntry(string oldFilePath, string newFilePath);

    void SetDisplayTitle(string filePath, string? displayTitle);

    // 라이브러리 목록에서만 제거한다(실제 파일은 건드리지 않음).
    void RemoveEntry(string filePath);

    // 항목이 없으면 빈 상태로 새로 만든다(열지는 않고 목록에만 등록할 때 사용, 예: 드래그 앤 드롭 추가).
    void EnsureEntryExists(string filePath);
}
