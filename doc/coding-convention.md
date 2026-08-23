# coding-convention.md

C# / WPF / MVVM 프로젝트 공통 코딩 컨벤션.

## 네이밍 규칙

| 대상 | 규칙 | 예시 |
|---|---|---|
| 클래스, 인터페이스 | PascalCase | `ReaderViewModel`, `IFileService` |
| 메서드 | PascalCase | `LoadBook()`, `SaveSettingsAsync()` |
| 공개 프로퍼티 | PascalCase | `FontSize`, `CurrentPage` |
| private 필드 | `_camelCase` | `_currentBook`, `_settingsService` |
| 지역 변수, 매개변수 | camelCase | `filePath`, `pageIndex` |
| 상수 | PascalCase | `DefaultFontSize` |
| 인터페이스 | `I` 접두사 | `IEncodingDetector` |
| 비동기 메서드 | `Async` 접미사 | `LoadFileAsync()` |

## 파일/폴더 구조 규칙

- 1 파일 = 1 클래스 원칙
- ViewModel은 대응하는 View와 이름을 맞춘다 (`ReaderView.xaml` ↔ `ReaderViewModel.cs`)
- Service는 인터페이스(`IXxxService`)와 구현체(`XxxService`)를 분리

## MVVM 규칙

- View의 코드비하인드(`.xaml.cs`)에는 UI 이벤트를 ViewModel 커맨드로 연결하는 최소 코드만 작성
- 비즈니스 로직은 ViewModel 또는 Service에 위치, View에 두지 않음
- ViewModel은 `INotifyPropertyChanged` 구현 (또는 `CommunityToolkit.Mvvm`의 `ObservableObject` 사용 검토 가능)
- Command는 `ICommand` 구현 (또는 `RelayCommand` 패턴 사용)

## 비동기 처리

- 파일 I/O, JSON 저장/로드 등은 가능한 `async`/`await` 사용
- UI 스레드 블로킹 방지 (특히 대용량 텍스트 파일 로딩 시)

## 주석 / 문서화

- public 클래스와 메서드에는 간단한 XML 주석(`///`) 권장 (복잡한 로직에 한해)
- 자명한 코드에는 과도한 주석 지양

## Git 커밋 규칙

- 커밋 메시지는 한글 또는 영어 모두 허용, 변경 내용을 명확히 기술
- 예: `feat: 페이지 넘김 키보드 입력 추가`, `fix: EUC-KR 인코딩 감지 오류 수정`
- 지시서(`CLAUDE.md`, `doc/*.md`) 변경 시 별도 커밋으로 분리 권장
- **커밋 메시지는 Claude Code가 변경 내용을 보고 직접 작성한다.** 작업 단위(마일스톤/기능/버그 수정 등)가 끝나면 사용자 확인 없이 바로 커밋하고 `origin`에 푸시한다. 단, 민감할 수 있는 강제 작업(force push, 히스토리 재작성 등)은 예외로 항상 확인을 구한다.

## 코드 포맷팅

- Visual Studio / VS Code 기본 C# 포맷터(`dotnet format`) 사용
- 들여쓰기: 스페이스 4칸
- 중괄호는 새 줄에서 시작 (Allman 스타일, C# 기본 관례)
