namespace TextReaders.Models;

public sealed class Book
{
    public required string FilePath { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
}
