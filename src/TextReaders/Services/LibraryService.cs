using System.IO;
using TextReaders.Models;

namespace TextReaders.Services;

public sealed class LibraryService : ILibraryService
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "data", "library.json");

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
