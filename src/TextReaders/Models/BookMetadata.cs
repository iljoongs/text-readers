namespace TextReaders.Models;

public sealed class BookMetadata
{
    public required string Title { get; set; }

    public required string Author { get; set; }

    public DateTime AddedAt { get; set; }

    public required string Sha256 { get; set; }
}
