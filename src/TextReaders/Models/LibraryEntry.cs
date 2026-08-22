namespace TextReaders.Models;

public sealed class LibraryEntry
{
    public required string FilePath { get; set; }

    public int LastPageIndex { get; set; } = 1;
}
