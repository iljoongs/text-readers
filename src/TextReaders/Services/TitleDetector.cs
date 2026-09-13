using System.Text.RegularExpressions;

namespace TextReaders.Services;

// 본문 맨 첫 줄이 "제목: 실제 제목" 형태면 그것을 책의 진짜 제목으로 인식한다.
// 정확성을 위해 문서 전체가 아니라 첫 줄만 검사한다(본문 중간에 우연히 등장하는 "제목:" 오탐 방지).
public static class TitleDetector
{
    private static readonly Regex TitleLineRegex = new(@"^\s*제목\s*[:：]\s*(.+?)\s*$");

    public static string? DetectTitle(string content)
    {
        var firstLine = GetFirstLine(content);
        var match = TitleLineRegex.Match(firstLine);
        if (!match.Success)
        {
            return null;
        }

        var title = match.Groups[1].Value.Trim();
        return title.Length > 0 ? title : null;
    }

    private static string GetFirstLine(string content)
    {
        var newlineIndex = content.IndexOfAny(['\r', '\n']);
        return newlineIndex >= 0 ? content[..newlineIndex] : content;
    }
}
