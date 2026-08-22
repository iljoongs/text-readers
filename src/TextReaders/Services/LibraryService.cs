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

    public int GetLastPageIndex(string filePath)
    {
        var data = JsonFileStore.Load(FilePath, () => new LibraryData());
        var entry = data.Entries.FirstOrDefault(e => e.FilePath == filePath);
        return entry?.LastPageIndex ?? 1;
    }

    public void UpdatePosition(string filePath, int pageIndex)
    {
        var data = JsonFileStore.Load(FilePath, () => new LibraryData());

        var entry = data.Entries.FirstOrDefault(e => e.FilePath == filePath);
        if (entry is null)
        {
            entry = new LibraryEntry { FilePath = filePath };
            data.Entries.Add(entry);
        }

        entry.LastPageIndex = pageIndex;
        data.LastOpenedFilePath = filePath;

        JsonFileStore.Save(FilePath, data);
    }
}
