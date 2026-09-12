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

    // 데이터 폴더 위치가 바뀐 뒤, 새 위치를 기준으로 사용자 폰트 목록을 다시 읽는다.
    void RescanCustomFonts();
}
