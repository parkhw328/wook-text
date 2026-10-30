# wText 개발 안내

[프로젝트 소개](../README.md) · [기능 및 사용법](FEATURES.md)

## 개발 명령

Windows x64의 PowerShell에서 저장소 루트를 기준으로 실행합니다. 개발 SDK는 `global.json`에 고정되어 있습니다. 인터넷 연결은 도구·패키지 최초 다운로드에 필요합니다.

```powershell
.\scripts\bootstrap.ps1                 # .tools에 .NET SDK와 NSIS 설치, 체크섬 확인
.\scripts\build.ps1                     # 잠금 파일 복원, Release 빌드, 단위·WPF 검증
.\scripts\run.ps1                       # 개발 모드 실행
.\scripts\package.ps1                   # 런타임 포함 설치 EXE 및 무설치 ZIP 생성
.\scripts\package.ps1 -SmokeInstaller   # 격리된 설치 검증용 패키지 생성
.\scripts\test-installer.ps1            # 설치·재설치·제거 및 사용자 파일 보존 검증
.\scripts\make-icon.ps1                 # 벡터 원본에서 PNG와 다중 크기 ICO 재생성
.\scripts\update-screenshots.ps1        # WPF 검증 결과에서 기능 안내 화면 갱신
```

**현재는 사용자 요청으로 추가 CI 실행을 중단한 상태입니다.** CI 사용 재개 지시가 있을 때 `gh workflow run package.yml`로 GitHub Actions의 **Windows distribution**을 수동 실행할 수도 있습니다. Windows에서 단위·WPF 검사, 설치·제거 검사 후 설치 파일·ZIP·체크섬을 `windows-distribution` 아티팩트로, 화면을 `windows-verification`으로 30일간 보관합니다. 공개 릴리스는 자동 생성하지 않습니다.

패키징 결과는 `artifacts/installer/`의 설치 파일과 `artifacts/`의 무설치 ZIP에 생성됩니다. 게시 전 실행 파일은 `artifacts/publish/win-x64/wText.exe`입니다. 새 버전 배포 시 설치 파일·ZIP·`SHA256SUMS.txt`를 해당 GitHub 릴리스에 첨부하고 README의 다운로드 버전과 링크를 함께 갱신하세요.

빌드·실행·패키징 스크립트는 `prepare-formatter.ps1`로 고정 버전 Node.js 26.10.0과 Prettier 3.9.6을 준비하고 체크섬을 확인합니다. 최초 준비에는 인터넷 연결과 Windows의 `tar`가 필요합니다. 직접 `dotnet build`를 실행할 때는 먼저 `.\scripts\prepare-formatter.ps1`을 실행하세요. 런타임은 앱 전용 폴더에 포함되므로 배포 용량이 증가합니다.

시스템 SDK 대신 `.tools/dotnet/dotnet.exe`를 사용할 수 있습니다.

```powershell
.\.tools\dotnet\dotnet.exe test tests/WookText.Core.Tests -c Release
.\.tools\dotnet\dotnet.exe format WookText.slnx --verify-no-changes
```

PowerShell 실행 정책 때문에 스크립트가 차단되면 명령별로 `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1`처럼 실행할 수 있습니다. 시스템 정책을 변경할 필요는 없습니다. 빌드와 패키징은 같은 `obj` 폴더를 사용하므로 동시에 실행하지 마세요.

설치 검증은 설치된 바이너리·글꼴에서도 WPF 검증을 실행하며 실제 사용자 설정을 건드리지 않습니다. 이전 검증용 설치 파일이 있다면 `test-installer.ps1 -PreviousSmokeInstaller artifacts/installer/wText-1.0.0-win-x64-setup-smoke.exe`로 업그레이드도 확인할 수 있습니다.

Linux에서도 README·변경 이력·화면·릴리스 설명을 같은 기준으로 갱신합니다. 환경별 검증 범위와 배포 마무리 절차는 [배포 가이드](RELEASING.md)를 따르세요.

## 프로젝트 구조

| 경로 | 역할 |
| --- | --- |
| `src/WookText.App/` | WPF 화면, 편집·비교·설정·라이선스, `SessionCoordinator` 작업 보관, 테마·구문 정의 |
| `src/WookText.App/Explorer/` | 탐색기 화면·상태, 지연 로딩·자동 갱신, 파일명 입력창 |
| `src/WookText.App/Formatting/` | 내장 Prettier 실행 진입점; 고정 버전 도구는 `.tools/formatter/`에서 빌드 시 복사 |
| `src/WookText.App/Assets/` | 앱 아이콘, 기본 글꼴과 출처·체크섬 |
| `src/WookText.Core/` | 파일·인코딩·비교, `WorkspaceFiles` 탐색·파일 작업, `SessionStore` 복구본과 설정 저장 |
| `tests/WookText.Core.Tests/` | xUnit 파일·인코딩·비교·검색·탐색기·설정 회귀 테스트 |
| `tests/WookText.App.SmokeTests/` | 실제 WPF 컨트롤 동작 검증과 화면 렌더링 |
| `installer/`, `scripts/` | NSIS 설치 정의 및 개발·배포 자동화 |
| `rules/rules.md` | 제품 결정과 개발 규칙 |
| `docs/REQUIREMENTS.md` | 요구사항 분석, 수용 기준, 후속 범위 |
| `docs/RELEASING.md` | 현재 배포 상태, 환경별 검증 범위, 문서·패키지 마무리 절차 |
| `docs/images/` | 기능 안내의 실제 앱 화면 |
| `licenses/` | 의존성 라이선스 원문 |
| `artifacts/` | 실행 파일, 설치 패키지, 화면 검증 결과; Git 제외 |

## 기여 및 버전 관리

개발 지침은 `AGENTS.md`와 `rules/rules.md`, 버전 기록은 `CHANGELOG.md`를 확인하세요. C#은 공백 4칸과 `.editorconfig`를 따르며 `dotnet format`을 사용합니다. 테스트명은 `Method_Condition_ExpectedResult` 형식입니다. 수치 기반 커버리지 기준은 아직 없으며 파일 손상 방지와 기능별 회귀 검증을 우선합니다.

커밋은 `Add encoding preservation tests`처럼 변경 의도가 드러나는 짧은 명령형으로 작성합니다. PR에는 관련 요구사항, 검증 결과, UI 변경 화면을 포함하세요.

## 미니맵·블록 편집 검증 (1.6.0)

`EditorDocument.View`는 본문과 `EditorMinimap`을 함께 배치한다. 미니맵은 문서·크기 변경 때만 표본 그림을 다시 만들고 스크롤·커서는 덧그린다. 탭 언로드 시 이벤트 구독을 해제한다. `EditorEditing`은 블록 클립보드와 줄 편집, `MainWindow.Editing.cs`는 명령·메뉴·본문 전용 단축키 연결을 담당한다.

`NavigationEditingChecks`는 WPF 실행에서 블록 끝 확장, 빈 행, 행 수 불일치, Ctrl+V 명령 경로, CR/LF/CRLF, 줄 이동과 단일 실행 취소, 미니맵 표시·숨김·탐색을 검사하고 `artifacts/smoke/minimap.png`를 만든다. Windows에서 `scripts/build.ps1` 성공 후 `scripts/update-screenshots.ps1`로 실제 화면을 복사하고 검토한다. Linux 교차 컴파일만으로 이 검사가 실행된 것으로 간주하지 않는다.

`docs/USER-GUIDE.md`는 `WookText.Core`의 내장 리소스다. `UserGuide`가 주제와 검색을 제공하고 `HelpWindow`가 본문을 표시한다. 설명서 수정 시 Core를 다시 빌드·패키징한다. `UserGuideTests`는 오프라인 리소스·주제·검색을 검증하며 `HelpChecks`는 WPF 명령 경로·검색·창 재사용·소유 창 종료·원문 보호와 `user-guide.png` 생성을 담당한다.

`MinimapLayout`은 미니맵 행 간격·현재 화면 범위·포인터 역변환의 공통 좌표 계산을 담당한다. `MinimapLayoutTests`는 짧은/긴 문서·작은 창·리사이즈·여백 좌표를 Linux에서도 검증한다. WPF 검사의 `minimap-short.png`는 짧은 문서 화면을 생성하며 Windows에서 실제 결과를 확인해야 한다.
