namespace TextReaders.Models;

public sealed class BookIndexEntry
{
    public required string Title { get; set; }

    public required string Author { get; set; }

    public DateTime AddedAt { get; set; }
}
