# wText

Windows x64용 무료 텍스트 편집기입니다. **0.3.0**은 파일 검색·생성·이름 변경과 자동 갱신을 지원하는 프로젝트 탐색기를 제공합니다. 블랙·오렌지 테마, 다국어 구문 강조, 글꼴 설정, 집중 모드와 두 파일 비교도 사용할 수 있습니다. 제작자는 **Hyunwook Park**이며 [GitHub 프로젝트](https://github.com/parkhw328/wook-text)에서 소스와 의견을 확인할 수 있습니다.

## 최신 버전 다운로드

**wText 0.3.0 · Windows x64** — [최신 릴리스 및 변경 내용](https://github.com/parkhw328/wook-text/releases/latest)

| 배포 형태 | 다운로드 | 실행 방법 |
| --- | --- | --- |
| 설치형 | [Windows 설치 파일 (.exe)](https://github.com/parkhw328/wook-text/releases/download/v0.3.0/wText-0.3.0-win-x64-setup.exe) | 다운로드한 파일을 실행해 설치 |
| 무설치형 | [무설치 ZIP](https://github.com/parkhw328/wook-text/releases/download/v0.3.0/wText-0.3.0-win-x64-portable.zip) | **전체 압축 해제**한 뒤 `wText.exe` 실행 |

[SHA-256 체크섬](https://github.com/parkhw328/wook-text/releases/download/v0.3.0/SHA256SUMS.txt) · [전체 버전 목록](https://github.com/parkhw328/wook-text/releases)

배포본에는 .NET 런타임이 포함됩니다. 설치 마법사는 현재 사용자 폴더와 시작 메뉴에 설치하며 관리자 권한이 필요하지 않습니다. Windows 설정의 앱 목록에서 제거할 수 있습니다. 기존 텍스트 파일 연결은 변경하지 않습니다.

## 현재 기능

- 다중 탭, 파일·폴더 드래그 앤 드롭, 실행 인자의 파일 열기
- 파일 형식별 아이콘·폴더 우선 자연 정렬, 비동기 프로젝트 탐색과 파일 이름·경로 검색
- 파일·폴더 생성과 이름 변경, 경로 복사, 현재 문서 위치 찾기, 외부 변경 자동 갱신
- 최근 작업 폴더, 마지막 폴더·펼침 상태 복원, 숨김·빌드·의존성 폴더 표시 선택
- wShell을 참고한 블랙·오렌지 테마, 테두리 없는 메뉴, 작업표시줄에서 구분되는 문서/T 아이콘
- 열린 문서·탐색기 개별 접기, 사이드바 전체 숨기기·너비 조절, F11 집중 모드
- 영문·숫자 JetBrains Mono + 한글 Noto Sans KR 기본 글꼴 동봉; 설치된 글꼴 선택과 크기 미리보기
- 줄 번호, 자동 줄바꿈, 공백 표시, 글자 크기 조절, 실행 취소·다시 실행
- YAML, HTML, JavaScript, TypeScript, JSP, Java, JSON, XML, CSS, C#, C/C++, Python, SQL, PowerShell, Shell, INI/TOML/Properties, Markdown, PHP 및 일반 텍스트 모드
- 일반 텍스트 검색, 대소문자 구분, 바꾸기·모두 바꾸기
- UTF-8, BOM이 있는 UTF-16/32, 사용자 확인 후 CP949 열기와 원본 인코딩·줄바꿈 보존
- 저장되지 않은 문서의 닫기 확인, 외부 변경 감지, 임시 파일을 통한 원본 교체 저장
- 두 파일의 추가·삭제·수정 줄 및 단어 표시, 스크롤 동기화, 변경 구간 이동, 공백 옵션, 줄바꿈·인코딩 차이 안내

비교 화면은 디스크에 저장된 파일을 읽습니다. 편집 중인 변경은 먼저 저장하세요. 파일 열기·저장은 16 MiB, 비교는 파일당 500,000자·20,000줄까지 지원합니다. 자동 복구, 정규식 검색, 다중 커서, 미니맵, 비교 병합은 아직 제공하지 않습니다.

## 프로젝트 탐색기

`Ctrl+Shift+O` 또는 폴더 드롭으로 작업 폴더를 엽니다. 화살표 키로 이동하고 `Enter`나 더블 클릭으로 파일을 여세요. `Ctrl+P`는 파일 이름·상대 경로를 검색합니다. 예를 들어 `src editor`는 경로에 두 단어가 모두 포함된 파일을 찾습니다. 결과에서 `Enter`로 열고 `Esc`로 검색을 지웁니다.

탐색기 툴바에서 **새 파일·새 폴더·새로고침·모두 접기·현재 문서 찾기**를 실행할 수 있습니다. 우클릭 메뉴에는 이름 변경(`F2`), 상대·전체 경로 복사, Windows 탐색기에서 보기가 있습니다. 이름을 바꾸면 열린 문서의 경로도 바뀌며 미저장 내용과 실행 취소 기록을 유지합니다. 외부 파일 추가·삭제·이름 변경은 자동 반영하며 `F5`로 직접 갱신할 수도 있습니다.

**···** 메뉴에서 최근 작업 폴더 6개와 표시 옵션을 관리합니다. `.git`, `node_modules`, `bin`, `obj` 등의 생성 폴더와 Windows 숨김 항목은 기본적으로 제외하며 다시 표시할 수 있습니다. 작업 폴더를 닫아도 열린 문서는 유지됩니다. 마지막 작업 폴더·펼친 폴더 최대 200개·필터는 `%LocalAppData%\wText\settings.workspace.json`에 저장합니다.

폴더는 필요할 때 불러오고 보이는 행 위주로 렌더링합니다. 폴더 하나당 파일·하위 폴더 합계 10,000개까지 표시하며, 파일 검색은 최대 100,000개 항목을 확인해 결과 200개까지 보여줍니다. 한도 도달 시 안내하므로 검색어를 구체화하세요. 연결 폴더는 자동으로 순회하지 않습니다. 검색 대상은 파일 이름·경로이며 파일 내용 전체 검색은 후속 범위입니다.

## 편집 환경 설정

상단 **Aa**, 상태 표시줄의 글자 크기 또는 `Ctrl+,`로 설정을 엽니다. 한글·영문 글꼴을 따로 선택하고 9–48 px 범위에서 미리 본 뒤 적용하세요. 모든 탭과 비교창에 함께 반영되며 문서 내용·커서·실행 취소 기록은 유지됩니다. 비교창은 줄 정렬을 위해 자동 줄바꿈을 사용하지 않습니다.

확장자로 언어를 감지하며, **언어** 메뉴나 상태 표시줄에서 직접 바꿀 수 있습니다. 새 문서도 저장 전에 언어를 지정할 수 있고 **자동 감지**로 돌아갈 수 있습니다. 구문 강조는 어휘 기반이며 자동 완성이나 컴파일 오류 검사는 제공하지 않습니다.

글꼴·크기·편집 옵션과 사이드바 너비·표시·접힘 상태는 `%LocalAppData%\wText\settings.json`에 저장합니다. 설치형과 무설치형이 같은 사용자 설정을 사용합니다. 집중 모드는 일시적이며, 종료하면 기존 사이드바 표시 상태를 복원합니다. **도움말 → wText 정보**에는 제작자, 버전, GitHub 링크와 주소 복사가 있습니다.

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
```

패키징 결과는 `artifacts/installer/`의 설치 파일과 `artifacts/`의 무설치 ZIP에 생성됩니다. 게시 전 실행 파일은 `artifacts/publish/win-x64/wText.exe`입니다. 새 버전 배포 시 설치 파일·ZIP·`SHA256SUMS.txt`를 해당 GitHub 릴리스에 첨부하고 위 다운로드 버전과 링크를 함께 갱신하세요.

시스템 SDK 대신 `.tools/dotnet/dotnet.exe`를 사용할 수 있습니다.

```powershell
.\.tools\dotnet\dotnet.exe test tests/WookText.Core.Tests -c Release
.\.tools\dotnet\dotnet.exe format WookText.slnx --verify-no-changes
```

PowerShell 실행 정책 때문에 스크립트가 차단되면 명령별로 `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1`처럼 실행할 수 있습니다. 시스템 정책을 변경할 필요는 없습니다. 빌드와 패키징은 같은 `obj` 폴더를 사용하므로 동시에 실행하지 마세요.

설치 검증은 설치된 바이너리·글꼴에서도 WPF 검증을 실행하며 실제 사용자 설정을 건드리지 않습니다. 이전 검증용 설치 파일이 있다면 `test-installer.ps1 -PreviousSmokeInstaller artifacts/installer/wText-0.2.0-win-x64-setup-smoke.exe`로 업그레이드도 확인할 수 있습니다.

## 프로젝트 구조

| 경로 | 역할 |
| --- | --- |
| `src/WookText.App/` | WPF 화면, 편집·비교·설정, 테마와 `SyntaxDefinitions/` 구문 정의 |
| `src/WookText.App/Explorer/` | 탐색기 화면·상태, 지연 로딩·자동 갱신, 파일명 입력창 |
| `src/WookText.App/Assets/` | 앱 아이콘, 기본 글꼴과 출처·체크섬 |
| `src/WookText.Core/` | 파일 저장·인코딩·텍스트 비교, `WorkspaceFiles` 탐색·파일 작업, 설정 저장 |
| `tests/WookText.Core.Tests/` | xUnit 파일·인코딩·비교·검색·탐색기·설정 회귀 테스트 |
| `tests/WookText.App.SmokeTests/` | 실제 WPF 컨트롤 동작 검증과 화면 렌더링 |
| `installer/`, `scripts/` | NSIS 설치 정의 및 개발·배포 자동화 |
| `resources/settings.txt` | 원본 요구사항 |
| `rules/rules.md` | 제품 결정과 개발 규칙 |
| `docs/REQUIREMENTS.md` | 요구사항 분석, 수용 기준, 후속 범위 |
| `licenses/` | 의존성 라이선스 원문 |
| `artifacts/` | 실행 파일, 설치 패키지, 화면 검증 결과; Git 제외 |

## 단축키

| 단축키 | 동작 |
| --- | --- |
| `Ctrl+N`, `Ctrl+O`, `Ctrl+S` | 새 문서, 열기, 저장 |
| `Ctrl+Shift+S`, `Ctrl+W` | 다른 이름으로 저장, 탭 닫기 |
| `Ctrl+F`, `Ctrl+H`, `F3` | 찾기, 바꾸기, 다음 찾기 |
| `Ctrl+Z`, `Ctrl+Y` | 실행 취소, 다시 실행 |
| `Ctrl+Tab`, `Ctrl+Shift+Tab` | 다음·이전 탭 |
| `Ctrl+Shift+D` | 두 파일 비교 |
| `Ctrl+Shift+O`, `Ctrl+P` | 작업 폴더 열기, 파일 이름·경로 검색 |
| `Ctrl+Shift+E` | 탐색기에서 현재 문서 찾기 |
| `F2`, `F5` | 탐색기에서 이름 변경, 새로고침 |
| `Ctrl+Shift+C` | 탐색기에서 선택 항목의 전체 경로 복사 |
| `Ctrl+B` | 사이드바 접기·펼치기 |
| `F11`, `Esc` | 집중 모드 전환, 검색 닫기·집중 모드 나가기 |
| `Ctrl+,` | 글꼴 및 편집 설정 |
| `Ctrl+마우스 휠`, `Ctrl++`, `Ctrl+-` | 글자 크게·작게 |
| `Ctrl+0` | 기본 글자 크기 15 px |

## 기여 및 버전 관리

개발 지침은 `AGENTS.md`와 `rules/rules.md`, 버전 기록은 `CHANGELOG.md`를 확인하세요. C#은 공백 4칸과 `.editorconfig`를 따르며 `dotnet format`을 사용합니다. 테스트명은 `Method_Condition_ExpectedResult` 형식입니다. 수치 기반 커버리지 기준은 아직 없으며 파일 손상 방지와 기능별 회귀 검증을 우선합니다.

커밋은 `Add encoding preservation tests`처럼 변경 의도가 드러나는 짧은 명령형으로 작성합니다. PR에는 관련 요구사항, 검증 결과, UI 변경 화면을 포함하세요.

## 라이선스

자체 코드는 [MIT](LICENSE)입니다. AvalonEdit(MIT), DiffPlex(Apache-2.0), JetBrains Mono·Noto Sans KR(OFL-1.1), Flexoki(MIT), .NET/WPF, NSIS의 고지는 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)를 확인하세요. 글꼴은 앱 폴더에서 직접 불러오며 Windows에 별도로 설치하지 않습니다. Sublime Text와 Notepad++는 사용성 참고 대상이며 해당 제품의 코드나 이미지를 포함하지 않습니다.
