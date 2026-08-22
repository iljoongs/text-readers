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
│   └── library.json                # 열었던 파일 목록, 마지막 읽은 위치
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
- 저장 위치: 실행 폴더 기준 `./data/*.json`
- 저장 시점: 설정 변경 시, 페이지 이동 시(마지막 읽은 위치), 앱 종료 시
- **향후 암호화 계획**: `System.Security.Cryptography.Aes`로 JSON 텍스트를 암호화하여 `.dat` 확장자로 저장하는 방식으로 전환 예정 (Phase 4, `doc/roadmap.md` 참고). 지금 단계에서는 평문 JSON으로 충분하며, 저장 구조 설계 시 "직렬화 대상 객체"와 "파일 I/O 로직"을 분리해 두어야 나중에 암호화 레이어만 끼워 넣기 쉽다.

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
