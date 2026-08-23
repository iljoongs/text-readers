using System.Windows.Media;

namespace TextReaders.Services;

public interface IFontService
{
    IReadOnlyList<string> GetAvailableFontFamilyNames();

    FontFamily ResolveFontFamily(string familyName);

    // 파일 선택 다이얼로그를 띄워 폰트를 추가한다. 사용자가 취소하면 null.
    string? AddCustomFontFromDialog();

    // 이미 경로를 알고 있을 때(다이얼로그 없이) 폰트를 추가한다. 발견된 family name을 반환.
    string AddCustomFont(string sourceFilePath);
}
