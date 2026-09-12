using System.IO;
using System.Text.Json.Serialization;

namespace TextReaders.Models;

public sealed class LibraryEntry
{
    public required string FilePath { get; set; }

    // .mybook은 파일명이 SHA256 해시라 파일명에서 실제 제목을 얻을 수 없다.
    // mybook으로 저장/변경/열기할 때 채워지며, 없으면(txt/md/json) 파일명에서 그대로 뽑는다.
    public string? DisplayTitle { get; set; }

    [JsonIgnore]
    public string Title => !string.IsNullOrWhiteSpace(DisplayTitle) ? DisplayTitle : Path.GetFileNameWithoutExtension(FilePath);

    [JsonIgnore]
    public bool IsMyBook => Path.GetExtension(FilePath).Equals(".mybook", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsTextFormat => !IsMyBook;

    [JsonIgnore]
    public string FormatBadge => Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant();

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
