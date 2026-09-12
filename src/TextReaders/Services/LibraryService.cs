using System.IO;
using TextReaders.Models;

namespace TextReaders.Services;

public sealed class LibraryService : ILibraryService
{
    private static string FilePath => Path.Combine(AppPaths.DataDirectory, "library.json");

    public string? GetLastOpenedFilePath()
    {
        var data = JsonFileStore.Load(FilePath, () => new LibraryData());
        return data.LastOpenedFilePath;
    }

    public IReadOnlyList<LibraryEntry> GetAllEntries()
    {
        var data = JsonFileStore.Load(FilePath, () => new LibraryData());
        return data.Entries.OrderByDescending(e => e.LastOpenedAt).ToList();
    }

    public int GetLastPageIndex(string filePath)
    {
        var data = JsonFileStore.Load(FilePath, () => new LibraryData());
        var entry = data.Entries.FirstOrDefault(e => e.FilePath == filePath);
        return entry?.LastPageIndex ?? 1;
    }

    public void UpdatePosition(string filePath, int pageIndex)
    {
        var data = JsonFileStore.Load(FilePath, () => new LibraryData());
        var entry = GetOrCreateEntry(data, filePath);

        entry.LastPageIndex = pageIndex;
        entry.LastOpenedAt = DateTime.Now;
        data.LastOpenedFilePath = filePath;

        JsonFileStore.Save(FilePath, data);
    }

    public IReadOnlyList<Bookmark> GetBookmarks(string filePath)
    {
        var data = JsonFileStore.Load(FilePath, () => new LibraryData());
        var entry = data.Entries.FirstOrDefault(e => e.FilePath == filePath);
        return entry?.Bookmarks ?? new List<Bookmark>();
    }

    public void AddBookmark(string filePath, Bookmark bookmark)
    {
        var data = JsonFileStore.Load(FilePath, () => new LibraryData());
        var entry = GetOrCreateEntry(data, filePath);

        entry.Bookmarks.Add(bookmark);

        JsonFileStore.Save(FilePath, data);
    }

    public void RemoveBookmark(string filePath, Bookmark bookmark)
    {
        var data = JsonFileStore.Load(FilePath, () => new LibraryData());
        var entry = data.Entries.FirstOrDefault(e => e.FilePath == filePath);

        // JSON에서 새로 역직렬화된 인스턴스라 참조가 아니라 값으로 대상을 찾는다.
        entry?.Bookmarks.RemoveAll(b => b.PageNumber == bookmark.PageNumber && b.CreatedAt == bookmark.CreatedAt);

        if (entry is not null)
        {
            JsonFileStore.Save(FilePath, data);
        }
    }

    public IReadOnlyList<Highlight> GetHighlights(string filePath)
    {
        var data = JsonFileStore.Load(FilePath, () => new LibraryData());
        var entry = data.Entries.FirstOrDefault(e => e.FilePath == filePath);
        return entry?.Highlights ?? new List<Highlight>();
    }

    public void AddHighlight(string filePath, Highlight highlight)
    {
        var data = JsonFileStore.Load(FilePath, () => new LibraryData());
        var entry = GetOrCreateEntry(data, filePath);

        entry.Highlights.Add(highlight);

        JsonFileStore.Save(FilePath, data);
    }

    public void AddReadingTime(string filePath, double seconds)
    {
        var data = JsonFileStore.Load(FilePath, () => new LibraryData());
        var entry = GetOrCreateEntry(data, filePath);

        entry.TotalReadingSeconds += seconds;

        JsonFileStore.Save(FilePath, data);
    }

    public void MarkCompleted(string filePath)
    {
        var data = JsonFileStore.Load(FilePath, () => new LibraryData());
        var entry = GetOrCreateEntry(data, filePath);

        entry.IsCompleted = true;

        JsonFileStore.Save(FilePath, data);
    }

    // 번들(.json)을 열 때, 번들 안에 저장된 북마크/하이라이트/읽기 위치를 이 파일 경로의
    // 라이브러리 항목으로 그대로 옮겨 심는다 - 이후에는 일반 파일과 동일한 조회 경로(GetBookmarks 등)로 읽힌다.
    public void ImportEntry(string filePath, IReadOnlyList<Bookmark> bookmarks, IReadOnlyList<Highlight> highlights, int lastPageIndex)
    {
        var data = JsonFileStore.Load(FilePath, () => new LibraryData());
        var entry = GetOrCreateEntry(data, filePath);

        entry.Bookmarks = bookmarks.ToList();
        entry.Highlights = highlights.ToList();
        entry.LastPageIndex = lastPageIndex;

        JsonFileStore.Save(FilePath, data);
    }

    // Text > Edit Title로 파일을 rename한 뒤, 그 파일을 가리키던 라이브러리 항목과
    // "마지막으로 연 파일" 기록을 새 경로로 옮겨준다.
    public void RenameEntry(string oldFilePath, string newFilePath)
    {
        var data = JsonFileStore.Load(FilePath, () => new LibraryData());
        var entry = data.Entries.FirstOrDefault(e => e.FilePath == oldFilePath);
        if (entry is not null)
        {
            entry.FilePath = newFilePath;
        }

        if (data.LastOpenedFilePath == oldFilePath)
        {
            data.LastOpenedFilePath = newFilePath;
        }

        JsonFileStore.Save(FilePath, data);
    }

    private static LibraryEntry GetOrCreateEntry(LibraryData data, string filePath)
    {
        var entry = data.Entries.FirstOrDefault(e => e.FilePath == filePath);
        if (entry is null)
        {
            entry = new LibraryEntry { FilePath = filePath };
            data.Entries.Add(entry);
        }

        return entry;
    }
}
