# roadmap.md

Phase 1(MVP) 완료 이후 순차적으로 진행할 개발 계획. 지금 단계에서는 계획 수립까지만 하고, 실제 개발은 MVP 완료 후 착수한다.

## Phase 2 — 비주얼 (몰입감)

목표: "종이책을 읽는 느낌"을 실제로 구현.

- [x] **종이 질감 배경 / 다크모드 / 세피아모드**: `ReadingTheme`(Paper/Dark/Sepia) 하나로 통합 구현. 종이 질감은 라이선스 있는 외부 이미지 대신 Python(PIL/numpy/scipy `gaussian_filter(mode="wrap")`)으로 이음매 없이 생성한 절차적 텍스처(`Assets/Textures/paper.png`) 사용. Dark/Sepia는 단색 배경/텍스트 색상.
- [x] **페이지 넘김 애니메이션**: `RenderTargetBitmap`으로 전환 직전 화면을 캡처해 오버레이로 띄우고, 실제 페이지 전환은 그 뒤에서 즉시 실행한 뒤 `TranslateTransform`(`CubicEase` EaseOut) + 투명도로 오버레이를 슬라이드/페이드시키는 방식 채택. `FlowDocumentPageViewer`가 내부 렌더링을 직접 제어할 수 없어 PlaneProjection 3D 커얼 대신 이 방식을 선택 (구현/튜닝 리스크가 낮음).
- [x] **밝기 조절**: 오버레이 딤 처리(검정 `Rectangle` + 투명도 슬라이더, 0~0.7)로 구현. 실제 모니터 밝기 제어는 하지 않음.
- [ ] **페이지 넘김 사운드 효과** (선택적, on/off 토글) — 음원 파일을 새로 구해야 해서 이번 라운드에서는 제외, 필요 시 별도 진행.

## Phase 3 — 편의 기능

목표: 실제 독서 편의성 향상.

- [x] **읽기 진행률 표시**: 툴바에 "N / M (P%)" 텍스트
- [x] **북마크**: 여러 개 저장 가능(`LibraryEntry.Bookmarks`), 목록 Popup에서 이동/삭제. 페이지 번호 기반 저장이라 폰트/여백을 바꾸면 위치가 살짝 밀릴 수 있음(마지막 읽은 위치와 동일한 한계, 의도적 단순화)
- [x] **목차 자동 인식**: "제N장"/"Chapter N" 패턴을 문단 생성 루프에서 바로 감지. 이동은 `DynamicDocumentPaginator.GetPageNumber(paragraph.ContentStart)`로 실제 페이지 계산
- [x] **검색 기능**: 원본 텍스트가 아니라 이미 만들어진 FlowDocument의 문단들을 순회해 검색(줄바꿈 정규화로 인한 오프셋 불일치 원천 차단), 다음 찾기 + wrap
- [x] **하이라이트 / 메모**: 한 문단 안에서 선택한 구간만 지원(문단 간 선택은 범위 밖). 문단 오프셋+길이로 저장, 로드할 때마다 Run을 잘라 배경색/ToolTip으로 재적용. 같은 문단에 하이라이트가 여러 개면 문단별로 모아 한 번에 재구성(하나씩 따로 적용하면 서로 지워버리는 문제가 있어 수정)

## Phase 4 — 확장

목표: 라이브러리 관리 및 부가 기능. 외부 준비물/고위험 설계 결정이 필요 없는 5개(라이브러리 관리/읽기 통계/폰트 확장/TTS/`.md` 지원)를 1차로 진행, 데이터 암호화와 클라우드 동기화는 별도 논의 예정.

- [x] **라이브러리 관리**: `LibraryEntry` 목록을 `LibraryWindow`(모달리스, `ReaderViewModel` 공유)에 카드형 타일로 노출. 표지는 실제 이미지 대신 제목 문자열 해시 기반 파스텔 색상의 "가짜 표지"로 생성(별도 이미지 파일 없음)
- [x] **읽기 통계**: 30초 간격 `DispatcherTimer`로 누적 독서 시간 저장, `PageCount > 1 && CurrentPageNumber == PageCount`일 때 완독 처리. 라이브러리 창 상단에 총 독서 시간/완독 권수 합계 표시
- [x] **`.md` 파일 지원**: Markdig로 파싱한 AST를 `MarkdownFlowDocumentBuilder`가 FlowDocument로 변환(헤딩/문단/굵게·기울임/목록/코드/인용/구분선, 표·이미지는 범위 밖). 헤딩이 그대로 목차로 등록됨. 알려진 제한: 목록 항목의 Paragraph는 `List > ListItem > Paragraph`로 한 단계 더 중첩되어 있어 `Document.Blocks.OfType<Paragraph>()` 기반 검색이 목록 항목 안의 텍스트를 찾지 못함
- [ ] **데이터 암호화**: `System.Security.Cryptography.Aes`로 `data/*.json` → `.dat` 암호화 전환 (`architecture.md`의 "향후 암호화 계획" 참고) — 키 관리 설계가 필요해 별도 논의 예정
- [x] **TTS(음성 읽기) 연동**: Windows 내장 `System.Speech.Synthesis`(SAPI, 오프라인) 사용. 목차/검색과 동일한 "문단 → `GetPageNumber` → 페이지 이동" 패턴을 재사용해 재생 중인 문단이 바뀔 때마다 화면이 자동으로 따라감. 재생/일시정지/재개/정지 지원
- [x] **폰트 임베딩 확장**: `IFontService`가 내장 5종(`pack://`)과 `data/CustomFonts/`에 사용자가 추가한 폰트(파일 URI, `Fonts.GetFontFamilies`로 family name 자동 인식)를 통합 관리
- [ ] **클라우드 동기화**: Dropbox 연동을 통한 여러 기기 간 읽은 위치 공유 — Dropbox 개발자 앱 등록 등 사용자 쪽 선행 작업이 필요해 별도 논의 예정
- [x] **책 콘텐츠 저장 포맷(`.mybook`)**: `BookStorageService`로 본문을 표준 ZIP(`content.txt` + `metadata.json`)에 저장, 파일명은 SHA256 해시 앞 32자 + `.mybook`, `data/Books/index.json`으로 별도 인덱싱(`architecture.md`의 "책 콘텐츠 저장" 참고)
- [x] **데이터 폴더 위치 변경**: 설정 창에 "데이터 폴더" 섹션 추가, `찾아보기...`로 사용자가 원하는 폴더 지정 / `기본`으로 원래 위치(`AppContext.BaseDirectory\data`)로 복귀. `AppPaths`가 기존 데이터를 새 폴더로 옮기고 앱 재시작 없이 그 위치를 계속 사용(`architecture.md`의 "데이터 폴더 위치 변경" 참고)
- [x] **mybook을 File 메뉴/라이브러리에 연결**: File 메뉴에 `Open MyBook`/`Save as MyBook`/`Save MyBook As...` 추가. 라이브러리 카드는 한 번 클릭=선택, 더블클릭=열기로 바뀌었고, 오른쪽 클릭 팝업 메뉴로 "mybook으로 저장"(비파괴적, 원본 유지+신규 항목)/"mybook으로 변경"(같은 항목의 경로만 이전, 원본 파일은 유지) 지원. mybook이 아닌 항목은 카드에 형식 배지(TXT/MD/JSON) 표시(`architecture.md`의 "mybook과 File 메뉴/라이브러리 연동" 참고)

## 우선순위 원칙

- 각 Phase는 이전 Phase의 MVP/완료 기준을 만족한 뒤 시작한다.
- Phase 내에서도 세부 항목의 순서는 유동적으로 조정 가능하나, 이 문서를 갱신하여 변경 이력을 남긴다.
- 새로운 아이디어가 생기면 바로 구현하지 않고 이 로드맵에 먼저 추가한 뒤 우선순위를 논의한다.
