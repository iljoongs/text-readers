using System.IO;
using System.Text.Json.Serialization;

namespace TextReaders.Models;

public sealed class LibraryEntry
{
    public required string FilePath { get; set; }

    [JsonIgnore]
    public string Title => Path.GetFileNameWithoutExtension(FilePath);

    public int LastPageIndex { get; set; } = 1;

    public DateTime LastOpenedAt { get; set; }

    public List<Bookmark> Bookmarks { get; set; } = new();

    public List<Highlight> Highlights { get; set; } = new();
}
