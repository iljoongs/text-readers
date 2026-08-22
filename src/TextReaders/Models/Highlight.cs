namespace TextReaders.Models;

public sealed class Highlight
{
    public int ParagraphIndex { get; set; }

    public int StartOffset { get; set; }

    public int Length { get; set; }

    public string? Note { get; set; }
}
