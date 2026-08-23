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

    public double TotalReadingSeconds { get; set; }

    public bool IsCompleted { get; set; }

    [JsonIgnore]
    public string ReadingTimeDisplay
    {
        get
        {
            var totalMinutes = (int)(TotalReadingSeconds / 60);
            return totalMinutes >= 60
                ? $"{totalMinutes / 60}시간 {totalMinutes % 60}분"
                : $"{totalMinutes}분";
        }
    }

    public List<Bookmark> Bookmarks { get; set; } = new();

    public List<Highlight> Highlights { get; set; } = new();
}
