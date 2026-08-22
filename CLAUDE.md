# text-readers

Windows용 개인 텍스트(.txt) 북리더 애플리케이션. 종이 질감과 아름다운 한글 타이포그래피로 몰입감 있는 독서 경험을 제공하는 것이 핵심 목표.

## 프로젝트 개요

- **목적**: 개인용 로컬 .txt 파일 리더. 라이브러리/서버 없이 단일 실행 파일로 동작.
- **핵심 가치**: 기능성보다 "종이책을 읽는 느낌"의 시각적 완성도를 우선한다.
- **1차 파일 형식**: `.txt` (향후 `.md` 지원 계획, Phase 4 참고)

## 기술 스택

| 항목 | 선택 |
|---|---|
| 언어 | C# |
| UI 프레임워크 | WPF |
| 런타임 | .NET 8 (LTS) |
| 아키텍처 패턴 | MVVM |
| 인코딩 감지 | NuGet 패키지 (`UTF.Unknown` 사용, `doc/architecture.md` 참고) |
| 데이터 저장 | JSON (`./data` 폴더), 추후 AES 암호화 예정 |
| 페이지 넘김 방식 | 페이지형 (스크롤 아님) |

## 폴더 구조 (요약)

```
text-readers/
├── CLAUDE.md          # 이 파일 (메인 지시서)
├── text-readers.sln
├── .gitignore
├── data/                     # 런타임 데이터 (설정, 마지막 읽은 위치 등)
├── doc/
│   ├── architecture.md      # 폴더/레이어 구조, 라이브러리 상세
│   ├── mvp-spec.md          # Phase 1 기능 명세
│   ├── roadmap.md           # Phase 2~4 계획
│   └── coding-convention.md # 코딩 컨벤션
└── src/
    └── TextReaders/          # WPF 프로젝트 본체 (상세는 architecture.md)
```

## 개발 우선순위

1. **Phase 1 (MVP)** — 즉시 개발 착수. 상세 명세는 `doc/mvp-spec.md`
2. **Phase 2~4** — 계획만 수립, 개발은 Phase 1 완료 후 순차 진행. 상세는 `doc/roadmap.md`

## 문서 링크

- [아키텍처 & 폴더 구조](doc/architecture.md)
- [MVP 기능 명세](doc/mvp-spec.md)
- [개발 로드맵 (Phase 2~4)](doc/roadmap.md)
- [코딩 컨벤션](doc/coding-convention.md)

## 작업 원칙

- 이 지시서 세트(`CLAUDE.md` + `doc/*.md`)는 개발 방향을 정의하는 명세 문서다.
- 코드는 Claude Code가 작성/수정한다. 개발자는 방향 제시, 리뷰, 승인을 담당한다.
- 사양 변경 시 관련 문서를 함께 갱신하고 커밋 메시지에 변경 이유를 남긴다.
