namespace TextReaders.Models;

// Menu > File > Save/Save As로 저장하는 번들 형식. 원본 텍스트 + 그 시점의 표시 설정/
// 읽기 위치/북마크/하이라이트를 한 파일에 묶어, txt/md 원본이 없어도 그대로 이어 볼 수 있게 한다.
public sealed class ReaderBundle
{
    public required string Content { get; set; }

    public bool IsMarkdown { get; set; }

    public string FontFamilyName { get; set; } = string.Empty;

    public double FontSize { get; set; }

    public double LineSpacingMultiplier { get; set; }

    public MarginPreset MarginPreset { get; set; }

    public ReadingTheme Theme { get; set; }

    public double DimmingOpacity { get; set; }

    public int LastPageIndex { get; set; } = 1;

    public List<Bookmark> Bookmarks { get; set; } = new();

    public List<Highlight> Highlights { get; set; } = new();
}
