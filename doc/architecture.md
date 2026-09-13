# architecture.md

text-readers의 폴더 구조, 레이어 구성, 핵심 라이브러리 사용 방식을 정의한다.

## 폴더 구조 (전체)

```
text-readers/
├── CLAUDE.md
├── text-readers.sln
├── .gitignore
├── data/                          # 실행 시 생성/사용되는 로컬 데이터
│   ├── settings.json               # 폰트, 여백, 테마 등 사용자 설정
│   ├── library.json                # 열었던 파일 목록, 마지막 읽은 위치
│   └── Books/                      # BookStorageService가 생성하는 .mybook(ZIP) 파일 + index.json
├── doc/
│   ├── architecture.md
│   ├── mvp-spec.md
│   ├── roadmap.md
│   └── coding-convention.md
└── src/
    └── TextReaders/
        ├── TextReaders.csproj
        ├── App.xaml
        ├── App.xaml.cs
        ├── Assets/
        │   └── Fonts/               # 임베딩 한글 폰트 5종 (mvp-spec.md 참고)
        ├── Models/                  # 데이터 모델 (Book, ReadingPosition, Settings 등)
        ├── ViewModels/              # MVVM ViewModel
        ├── Views/                   # XAML 화면
        ├── Services/                # 파일 로딩, 인코딩 감지, JSON 저장 등
        └── Resources/               # 스타일, 텍스처 이미지 등
```

## MVVM 레이어 구성

- **Model**: 순수 데이터 구조체/클래스. UI 의존성 없음.
  - 예: `Book`, `ReadingPosition`, `AppSettings`
- **ViewModel**: View와 Model을 연결. `INotifyPropertyChanged` 구현.
  - 예: `ReaderViewModel`, `SettingsViewModel`
- **View**: XAML. 코드비하인드는 최소화하고 바인딩/커맨드로 처리.
- **Service**: 파일 I/O, 인코딩 감지, JSON 직렬화 등 순수 로직. ViewModel이 DI 또는 직접 인스턴스화로 호출.

## 핵심 라이브러리

### 인코딩 자동 감지: `UTF.Unknown`

- NuGet 패키지명: `UTF.Unknown` (v2.7.0 기준, DLL/네임스페이스명은 `UtfUnknown`)
- 사용 목적: `.txt` 파일 로드 시 UTF-8/EUC-KR 등 인코딩을 자동 판별
- 기본 사용 패턴 (`Services/EncodingDetectionService.cs` 참고):

```csharp
using UtfUnknown;

var result = CharsetDetector.DetectFromBytes(fileBytes);
Encoding encoding = result.Detected?.Encoding ?? Encoding.UTF8;
```

- EUC-KR 등 레거시 코드페이지를 `Encoding.GetEncoding`/`.Encoding` 프로퍼티로 정상 인식하려면 `System.Text.Encoding.CodePages` 패키지를 추가하고 앱 시작 시 `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`를 한 번 호출해야 한다 (.NET Core/5+는 기본적으로 레거시 코드페이지를 포함하지 않음).
- 감지 실패(`Detected`가 `null`) 시 UTF-8을 기본값으로 fallback 처리한다.

### 데이터 저장: JSON

- `System.Text.Json` 사용 (별도 패키지 불필요, .NET 기본 내장)
- 저장 위치: 기본값은 실행 폴더 기준 `./data/*.json`이지만, 설정 화면에서 다른 폴더로 변경 가능(아래 "데이터 폴더 위치 변경" 참고)
- 저장 시점: 설정 변경 시, 페이지 이동 시(마지막 읽은 위치), 앱 종료 시
- **향후 암호화 계획**: `System.Security.Cryptography.Aes`로 JSON 텍스트를 암호화하여 `.dat` 확장자로 저장하는 방식으로 전환 예정 (Phase 4, `doc/roadmap.md` 참고). 지금 단계에서는 평문 JSON으로 충분하며, 저장 구조 설계 시 "직렬화 대상 객체"와 "파일 I/O 로직"을 분리해 두어야 나중에 암호화 레이어만 끼워 넣기 쉽다.

### 데이터 폴더 위치 변경

- `Services/AppPaths.cs` — `SettingsService`/`LibraryService`/`FontService`(`CustomFonts`)/`BookStorageService`(`Books`)가 공통으로 사용하는 데이터 폴더 경로를 계산하는 정적 유틸리티. 각 서비스는 더 이상 `AppContext.BaseDirectory + "data"`를 직접 하드코딩하지 않고 `AppPaths.DataDirectory`를 매번 다시 읽는다(정적 캐싱 없음) → 폴더를 바꾼 뒤 앱 재시작 없이 즉시 반영됨
- 현재 데이터 폴더 위치를 가리키는 포인터는 데이터 폴더 "밖", 즉 실행 파일 옆의 `data-location.json`에 저장한다(데이터 폴더 안에 포인터를 두면 "포인터를 읽으려면 먼저 폴더 위치를 알아야 하는" 순환 문제가 생기기 때문). 포인터 파일이 없거나 값이 비어 있으면 기본값(`AppContext.BaseDirectory\data`)을 사용
- `AppPaths.ChangeDataDirectory(newDirectory)`: 기존 데이터 폴더의 모든 내용을 새 폴더로 복사한 뒤 기존 폴더를 삭제하고 포인터를 갱신한다. 새 경로가 기본값이면 포인터 값은 `null`로 저장(=기본값 사용). 새 경로가 현재 폴더 내부의 하위 경로면 예외를 던져 자기 자신 밑으로 이동하는 것을 막는다
- 설정 화면(`Views/SettingsWindow.xaml`)의 "데이터 폴더" 섹션에서 `찾아보기...`(`Microsoft.Win32.OpenFolderDialog`로 폴더 선택) / `기본`(기본 위치로 복귀) 두 버튼으로 조작. `ReaderViewModel`의 `BrowseDataDirectoryCommand`/`ResetDataDirectoryCommand`가 처리하며, 이동 후 `IFontService.RescanCustomFonts()`를 호출해 새 위치의 사용자 폰트 목록을 다시 읽는다
- `기본 폴더 데이터 복사` 버튼(`CopyDefaultDataCommand`) — `AppPaths.CopyDefaultDataToCurrentDirectory()`로 기본 폴더(`DefaultDataDirectory`)의 데이터를 "현재" 데이터 폴더로 복사(병합)한다. `ChangeDataDirectory`(이동)와 달리 기본 폴더는 그대로 남고, 겹치는 파일만 기본 폴더 쪽 내용으로 덮어쓰며 현재 폴더에만 있던 파일은 유지한다. 이미 기본 폴더를 사용 중이면 아무 것도 하지 않음(안내 메시지만 표시)

### 책 콘텐츠 저장: 표준 ZIP(`.mybook`)

- `Services/BookStorageService.cs` (+ `IBookStorageService`) — 책 본문 텍스트를 표준 ZIP 컨테이너로 저장/로드하는 저수준 저장 포맷 모듈. `ReaderViewModel`/File 메뉴/라이브러리 창에 연결되어 있다(아래 "mybook과 File 메뉴/라이브러리 연동" 참고).
- 압축은 `System.IO.Compression.ZipArchive`만 사용(독자 포맷 없음) → 확장자를 `.zip`으로 바꾸면 일반 압축 프로그램에서 그대로 열람 가능
- 파일명: 본문(UTF-8) SHA256 해시값 앞 32자(hex, 소문자) + `.mybook` 확장자, 저장 위치는 `data/Books/` — **내용이 바뀌면 해시도 바뀌므로 파일명이 곧 콘텐츠 버전**이다(경로 기반이 아닌 내용 기반 저장)
- ZIP 내부 구성: `content.txt`(본문, UTF-8, Deflate) + `metadata.json`(title/author/addedAt/sha256)
- 외부 인덱스: `data/Books/index.json`에 `해시 → { title, author, addedAt }` 매핑 유지, 저장/삭제 시 함께 갱신
- 암호화는 적용하지 않음 (평문 ZIP)

### mybook과 File 메뉴/라이브러리 연동

- **File 메뉴**: `Open MyBook`(`OpenMyBookCommand`, `.mybook` 필터 다이얼로그 → `ReaderViewModel.OpenPath`가 확장자로 분기), `Save as MyBook`(`SaveAsMyBookCommand`, 현재 책 제목으로 즉시 저장), `Save MyBook As...`(코드비하인드에서 `Views/MyBookSaveDialog`로 제목/저자를 물어본 뒤 저장) 세 항목을 `.json` 번들 Open/Save/Save As 아래에 추가했다. `ReaderViewModel.OpenPath`는 `.json`(번들) / `.mybook` / 그 외(txt·md) 세 갈래로 분기한다.
- **저장은 비파괴적**: `SaveCurrentAsMyBook`(File 메뉴)과 `SaveLibraryEntryAsMyBookCommand`(라이브러리 카드 오른쪽 클릭 메뉴 "mybook으로 저장")는 원본 항목을 그대로 두고 새 `.mybook` 파일을 만들어 라이브러리에 **별도 항목**으로 추가한다(북마크/하이라이트/마지막 페이지는 `ILibraryService.ImportEntry`로 그대로 옮겨 심음).
- **변경은 항목 자체를 이전**: 라이브러리 카드 오른쪽 클릭 메뉴 "mybook으로 변경"(`ConvertLibraryEntryToMyBookCommand`)은 같은 `LibraryEntry` 레코드의 `FilePath`만 새 `.mybook` 파일로 옮긴다(`ILibraryService.RenameEntry` 재사용) — **요구사항에 따라 원본 파일은 삭제하지 않고 그대로 둔 채 라이브러리 항목만 옮겨간다.**
- **제목 표시**: `.mybook`은 파일명이 해시라 파일명에서 제목을 뽑을 수 없다 - `Models/LibraryEntry.DisplayTitle`(신규 필드)에 실제 제목을 저장해두고 `Title` 프로퍼티가 `DisplayTitle ?? 파일명`으로 계산된다. mybook을 열 때마다(`OpenMyBookPath`) 인덱스에서 제목을 다시 읽어 `DisplayTitle`을 갱신한다.
- **형식 배지**: `LibraryEntry.IsMyBook`/`IsTextFormat`/`FormatBadge` 계산 프로퍼티로 mybook은 기본 카드 모습 그대로, 그 외 형식(txt/md/json)은 카드 우상단에 확장자 배지(`TXT`/`MD`/`JSON`)를 표시한다(`Views/LibraryWindow.xaml`).
- **카드 선택/더블클릭**: 라이브러리 창을 `ItemsControl`에서 `ListBox`(`SelectionMode="Single"`)로 바꿔 한 번 클릭은 카드 선택(테두리 강조), 더블클릭(`MouseLeftButtonDown` + `e.ClickCount == 2`)만 책을 연다. 오른쪽 클릭도 그 카드를 선택 상태로 만든 뒤(`PreviewMouseRightButtonDown`) 팝업 메뉴("mybook으로 저장"/"mybook으로 변경")를 띄운다.
- **mybook 특성으로 인한 기존 기능 보정**: `.mybook`은 파일명이 해시라 (1) `Text > Edit`로 내용을 바꾸면 해시가 바뀌므로 `UpdateContent`(`SaveMyBookContentInPlace`)가 제자리 덮어쓰기 대신 새 해시 파일을 만들고 라이브러리 항목을 그 파일로 옮긴 뒤 **이전 해시 파일은 `IBookStorageService.DeleteBook`으로 삭제**한다(편집할 때마다 옛 버전이 `data/Books/`에 쌓이지 않도록) — 내용이 그대로라 해시가 안 바뀌면 삭제하지 않는다, (2) `Text > Edit Title`은 실제 파일을 rename하는 대신(해시가 제목과 무관하므로) `DisplayTitle`만 갱신한다(`RenameCurrentFile`의 mybook 분기).

### 제목 자동 인식 ("제목: ...")

- `Services/TitleDetector.cs` — 본문 **맨 첫 줄**이 `제목: 실제 제목`(전각 콜론 `：`도 허용) 형태면 그 줄의 텍스트를 책의 진짜 제목으로 인식한다. 오탐 방지를 위해 문서 전체가 아니라 첫 줄만 검사한다(본문 중간에 우연히 등장하는 "제목:" 문자열은 무시).
- txt/md(`Services/FileService.cs`)와 json 번들(`ReaderViewModel.OpenBundle`)을 열 때 적용된다. `.mybook`은 저장 시점에 사용자가 직접 입력한 제목(`title`/`author` 다이얼로그, 인덱스)이 이미 있으므로 이 자동 인식을 적용하지 않는다.
- 인식된 제목은 `Book.Title`(읽는 동안의 제목)뿐 아니라 `ILibraryService.SetDisplayTitle`로 라이브러리 카드에도 반영된다. **파일을 열 때마다 다시 검사**하므로, "제목:" 줄을 지우고 다시 열면 표시 제목도 파일명으로 되돌아간다 — 반대로 파일명과 다른 제목을 계속 쓰고 싶다면 `Text > Edit`로 본문의 "제목:" 줄 자체를 고쳐야 하고, `Text > Edit Title`(파일명 변경)은 "제목:" 줄이 있는 파일에는 다음에 다시 열 때 덮어써진다.
- BOM 있는 UTF-8 파일을 열면 디코딩된 문자열 맨 앞에 `U+FEFF`가 남아 `^제목` 같은 첫 줄 패턴 매칭이 깨지던 문제를 함께 고쳤다(`FileService.LoadBook`에서 `TrimStart('﻿')`) — 기존 "제N장" 챕터 인식도 첫 문단이 BOM으로 시작하면 같은 문제가 있었다.

## 폰트 임베딩 방식

- `Assets/Fonts/` 폴더에 `.ttf`/`.otf` 파일을 프로젝트에 포함
- `.csproj`에서 `Resource` 또는 `Content`로 빌드 액션 지정
- XAML에서 `FontFamily="./Assets/Fonts/#폰트이름"` 형식으로 참조
- 폰트 목록은 `doc/mvp-spec.md`의 "폰트" 항목 참고

## 페이지 넘김 렌더링 방식 (Phase 1 vs Phase 2)

- **Phase 1**: 애니메이션 없이 즉시 전환 (텍스트 콘텐츠를 페이지 단위로 미리 분할해 두고 `Visibility` 또는 콘텐츠 교체로 처리)
- **Phase 2**: `RenderTransform`/`PlaneProjection` 등을 이용한 종이 넘김 애니메이션으로 고도화 (`doc/roadmap.md` 참고)

## 텍스트 페이지네이션 기본 원칙

- 어절 단위로 줄바꿈 (단어 중간 끊김 방지)
- 페이지 크기(가로/세로)와 폰트 크기에 따라 페이지당 글자 수를 동적으로 계산
- `FlowDocument` + `DocumentPaginator`를 활용하거나, 직접 페이지 분할 로직을 구현하는 두 가지 방식 중 선택 가능 — MVP 단계에서는 `FlowDocument` 기반 접근을 우선 검토
