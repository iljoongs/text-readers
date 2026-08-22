namespace TextReaders.Models;

public sealed class LibraryData
{
    public string? LastOpenedFilePath { get; set; }

    public List<LibraryEntry> Entries { get; set; } = new();
}
