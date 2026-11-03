# wText 개발 안내

[프로젝트 소개](../README.md) · [기능 및 사용법](FEATURES.md)

새 작업지시를 받으면 [프로젝트 규칙](../rules/rules.md)을 먼저 읽고, 이어서 [누적 요구사항](../rules/requirements.md)을 읽으세요. 새 규칙과 요구사항은 같은 작업에서 각 문서에 반영합니다. 에이전트 진입 지침은 [AGENTS.md](../AGENTS.md)에 있습니다.

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

빌드·실행·패키징 스크립트는 `prepare-formatter.ps1`로 고정 버전 Node.js 26.10.0과 Prettier 3.9.6을 준비하고 체크섬을 확인합니다. 최초 준비에는 인터넷 연결과 Windows의 `tar`가 필요합니다. 직접 `dotnet build`를 실행할 때는 먼저 `.\scripts\prepare-formatter.ps1`과 `.\scripts\prepare-webview.ps1`을 실행하세요. 후자는 공식 Microsoft CAB의 SHA-256을 확인하고 WebView2 Fixed Runtime 156.0.4314.58 x64 전체를 `.tools/webview2/runtime/`에 준비합니다. Windows `expand.exe`를 사용하며 캐시의 파일별 해시도 확인합니다. 런타임은 앱 전용 폴더에 포함되므로 배포 용량이 증가합니다.

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
| `src/WookText.App/Pdf/` | PDF 화면·선택·검색·편집 명령 및 입력 대화상자 |
| `src/WookText.App/Preview/` | 형식 선택·정적 HTML/CSS 렌더링, WebView2 격리·리소스 중계 |
| `src/WookText.Core/Pdf/` | PDFium C API, 직렬화된 엔진 접근, PDF 읽기·편집·원본 충돌 방지 저장 |
| `src/WookText.Core/DocumentPreview.cs` | 미리보기 형식 감지, CSV/JSON/XML/SVG 변환, 리소스 경계 |
| `scripts/prepare-webview.ps1` | WebView2 공식 CAB 체크섬·전체 추출·캐시 검증 |
| `tests/PdfFixture.cs` | Core와 WPF 검사가 공유하는 자체 생성 PDF 예제 |
| `licenses/PDFium/` | 고정 엔진 배포본의 모든 구성요소 고지 원문 |
| `src/WookText.App/Explorer/` | 탐색기 화면·상태, 지연 로딩·자동 갱신, 파일명 입력창 |
| `src/WookText.App/Formatting/` | 내장 Prettier 실행 진입점; 고정 버전 도구는 `.tools/formatter/`에서 빌드 시 복사 |
| `src/WookText.App/Assets/` | 앱 아이콘, 기본 글꼴과 출처·체크섬 |
| `src/WookText.Core/` | 파일·인코딩·비교, `WorkspaceFiles` 탐색·파일 작업, `SessionStore` 복구본과 설정 저장 |
| `tests/WookText.Core.Tests/` | xUnit 파일·인코딩·비교·검색·탐색기·설정 회귀 테스트 |
| `tests/WookText.PdfWorker.TestHost/` | PDF 프로세스 통신·비정상 종료·시간 초과·취소 검증용 콘솔 호스트 |
| `tests/generate-installer.test.cjs` | 설치 소유 목록·이전 파일 정리·경로 거부 회귀 검사 |
| `installer/legacy-files.txt`, `scripts/generate-installer.cjs` | 누적 패키지 소유 경로와 NSIS 현재/이전 파일 목록 생성 |
| `tests/WookText.App.SmokeTests/` | 실제 WPF 컨트롤 동작 검증과 화면 렌더링 |
| `installer/`, `scripts/` | NSIS 설치 정의 및 개발·배포 자동화 |
| `rules/rules.md` | 제품 결정과 개발 규칙 |
| `rules/requirements.md` | 누적 사용자 요구사항, 수용 기준, 변경 결정과 후속 범위 |
| `docs/REQUIREMENTS.md` | 요구사항 기준 문서의 새 위치 안내 |
| `docs/RELEASING.md` | 현재 배포 상태, 환경별 검증 범위, 문서·패키지 마무리 절차 |
| `docs/images/` | 기능 안내의 실제 앱 화면 |
| `licenses/` | 의존성 라이선스 원문 |
| `artifacts/` | 실행 파일, 설치 패키지, 화면 검증 결과; Git 제외 |

## 기여 및 버전 관리

개발 지침은 `AGENTS.md`와 `rules/rules.md`, 요구사항과 수용 기준은 `rules/requirements.md`, 버전 기록은 `CHANGELOG.md`를 확인하세요. C#은 공백 4칸과 `.editorconfig`를 따르며 `dotnet format`을 사용합니다. 테스트명은 `Method_Condition_ExpectedResult` 형식입니다. 수치 기반 커버리지 기준은 아직 없으며 파일 손상 방지와 기능별 회귀 검증을 우선합니다.

커밋은 `Add encoding preservation tests`처럼 변경 의도가 드러나는 짧은 명령형으로 작성합니다. PR에는 관련 요구사항, 검증 결과, UI 변경 화면을 포함하세요.

## 미니맵·블록 편집 검증 (1.6.0)

`EditorDocument.View`는 본문과 `EditorMinimap`을 함께 배치한다. 미니맵은 문서·크기 변경 때만 표본 그림을 다시 만들고 스크롤·커서는 덧그린다. 탭 언로드 시 이벤트 구독을 해제한다. `EditorEditing`은 블록 클립보드와 줄 편집, `MainWindow.Editing.cs`는 명령·메뉴·본문 전용 단축키 연결을 담당한다.

`NavigationEditingChecks`는 WPF 실행에서 블록 끝 확장, 빈 행, 행 수 불일치, Ctrl+V 명령 경로, CR/LF/CRLF, 줄 이동과 단일 실행 취소, 미니맵 표시·숨김·탐색을 검사하고 `artifacts/smoke/minimap.png`를 만든다. Windows에서 `scripts/build.ps1` 성공 후 `scripts/update-screenshots.ps1`로 실제 화면을 복사하고 검토한다. Linux 교차 컴파일만으로 이 검사가 실행된 것으로 간주하지 않는다.

`docs/USER-GUIDE.md`는 `WookText.Core`의 내장 리소스다. `UserGuide`가 주제와 검색을 제공하고 `HelpWindow`가 본문을 표시한다. 설명서 수정 시 Core를 다시 빌드·패키징한다. `UserGuideTests`는 오프라인 리소스·주제·검색을 검증하며 `HelpChecks`는 WPF 명령 경로·검색·창 재사용·소유 창 종료·원문 보호와 `user-guide.png` 생성을 담당한다.

`MinimapLayout`은 미니맵 행 간격·현재 화면 범위·포인터 역변환의 공통 좌표 계산을 담당한다. `MinimapLayoutTests`는 짧은/긴 문서·작은 창·리사이즈·여백 좌표를 Linux에서도 검증한다. WPF 검사의 `minimap-short.png`는 짧은 문서 화면을 생성하며 Windows에서 실제 결과를 확인해야 한다.


## PDF와 문서 미리보기 개발 (1.7.2 준비)

`EditorDocument`는 공통 탭의 경로·헤더·세션을 관리하고 PDF에는 `PdfDocumentView`를 배치한다. `ActiveTab`은 두 종류를 반환하고 `ActiveDocument`는 텍스트 탭만 반환해 기존 편집 명령이 PDF에 적용되지 않는다. PDF 화면에 들어가지 않는 AvalonEdit 인스턴스는 읽기 전용으로 두며 PDF 저장에는 사용하지 않는다.

`PdfEngine`은 PDFium의 C API를 직접 호출한다. 모든 네이티브 호출을 같은 잠금 안에서 실행하며 문서·페이지·비트맵·텍스트·주석·글꼴 핸들은 호출 안에서 해제한다. 편집은 원본 바이트를 복사해 연 임시 문서에 적용한 후 직렬화·재열기를 통과한 바이트만 반환한다. 화면은 페이지 단위이며 회전·CropBox 변환은 PDFium 좌표 API를 따른다. Form XObject 내부 편집·문단 재배치 등은 미지원이다. 글꼴을 교체한 텍스트는 원래 변환·크기·채움색을 이어받지만 폰트 폭과 객체 쌓임 순서가 달라질 수 있다.

NuGet `bblanchon.PDFium.Win32`와 `bblanchon.PDFium.Linux` 157.0.8100을 잠근다. Windows 배포에는 `pdfium.dll`이, Linux Core 테스트 출력에는 해당 Linux 네이티브 라이브러리가 필요하다. PDFium은 PDF 전용 네이티브 엔진이다. 문서 미리보기의 WebView2는 별도로 앱 안에 포함한다. `win-x64`는 앱 프로젝트에 지정되어 있다. 잠금 복원과 함께 Linux 교차 게시할 때는 `dotnet publish src/WookText.App -c Release --self-contained true -p:EnableWindowsTargeting=true -p:RestoreLockedMode=true`를 사용한다. 명령에 별도 `-r win-x64`를 덧붙이면 Core 프로젝트에도 RID가 전파되어 기존 잠금 파일과 불일치할 수 있다. 엔진 업그레이드 시 공식 배포본의 모든 라이선스와 헤더 ABI, Windows/Linux 실제 엔진 테스트를 함께 확인한다. `licenses/PDFium-NOTICES.txt`는 `licenses/PDFium/` 원문을 합친 앱 도움말용 UTF-8 고지다.

`DocumentPreview`는 형식 감지·표/구조 변환·리소스 경계를 담당하고 `MarkdownPreview`는 Markdig의 HTML 비활성화 파서를 사용한다. `DocumentPreviewView`는 350ms 유휴 후 배경 파싱, 최신 결과만 표시한다. 1.7.1의 WebView2CompositionControl은 로딩 종료 신고에 따라 1.7.2에서 표준 WPF WebView2로 바꿨다. 네이티브 컨트롤을 비동기 초기화의 오류 처리 범위 안에서 만들고, ProcessFailed 후 오류 표시에는 브라우저 메서드를 호출하지 않는다. 새로 고침 시 실패한 컨트롤/환경을 다시 만든다. 실제 Windows HWND 겹침·팝업·포커스와 화면은 추가 검증 대상이며 RenderTargetBitmap만으로 HWND 내부 화면을 캡처할 수는 없다. 스크립트·호스트 객체·웹 메시지를 꺼 둔 InPrivate 뷰이며 매 렌더링에 별도 가상 HTTPS 출처를 사용한다. 모든 요청을 가로채 현재 문서 또는 문서 폴더 안의 허용 CSS/이미지/글꼴만 반환한다. CSP와 탐색/권한/다운로드 차단을 함께 적용한다. 페이지의 JavaScript는 실행하지 않는다. 스크롤 위치 조회/복구에는 입력 문자열을 포함하지 않는 고정 호스트 명령만 사용한다. 원문/실행 취소는 변경하지 않으며 `PreviewVisible`·`PreviewKind`를 저장하고 이전 `MarkdownPreview` 세션 필드를 읽는다.

SDK `Microsoft.Web.WebView2` 1.0.4314.35는 BSD-3-Clause, Fixed Runtime 156.0.4314.58은 별도 Microsoft 약관이다. [공식 배포 안내](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution)에 따라 원본 CAB를 다운로드하고 압축 해제된 전체 파일을 `WebView2Runtime/`에 복사한다. 용량은 압축 전 약 675 MiB 증가한다. 설치형/포터블 모두 최초 미리보기에서 약관 동의를 받고 설정에 유지한다. 라이선스 원문·개인정보/SmartScreen 고지는 `licenses/WebView2-Runtime.txt`에 포함한다. 런타임 폴더의 `show_third_party_software_licenses.bat`는 Microsoft가 제공하는 내장 구성요소 고지 열기 도구다.

Windows 10에서는 공식 지침의 AppContainer SID 두 개에 런타임 폴더만 읽기/실행 권한을 부여한다. Windows 11에는 이 단계가 없다. 브라우저 샌드박스를 끄지 않는다. 사용자 프로필은 `%LocalAppData%/wText/preview-webview2`를 사용한다. 공유/UNC 경로의 고정 런타임은 지원하지 않는다. 고정 런타임은 자동 업데이트되지 않으므로 버전 변경 시 다운로드 URL·해시·SDK 잠금·약관·실제 Windows 실행을 함께 검토한다. 일반 웹 콘텐츠 요청 차단과 런타임 자체의 진단/SmartScreen 통신은 구분한다.

Linux에서 브라우저 CAB를 준비할 때는 같은 고정 URL과 SHA-256을 사용하고 CAB의 최상위 버전 디렉터리 아래 **모든** 파일을 `.tools/webview2/runtime/`에 보존해야 한다. Linux CAB 추출·교차 컴파일은 Windows PowerShell 준비 스크립트, ACL, WPF/브라우저 실행 검증을 대신하지 않는다.

검증 명령(저장소 루트, 사용 가능한 .NET 10 SDK):

```bash
dotnet test tests/WookText.Core.Tests -c Release --filter 'FullyQualifiedName~PdfTests|FullyQualifiedName~PdfWorkerTests|FullyQualifiedName~MarkdownPreviewTests|FullyQualifiedName~DocumentPreviewTests'
dotnet build tests/WookText.App.SmokeTests -c Release -p:EnableWindowsTargeting=true
```

Core 검사는 자체 생성 PDF에서 저장·재열기, 페이지 순서·회전·선택 좌표, 한글 글자, 이미지 교체/삭제, 주석·메모, 서명 읽기 전용·원본 충돌·혼합 세션을 확인한다. `PdfPreviewChecks`는 Windows WPF에서 PDF 명령·검색·클립보드·저장·실행 취소·재실행 복원과 Markdown 원문 유지, HTML 로컬 CSS 배치·스크립트 비활성화·형식 복원, 기본/최소 화면을 확인한다. Linux에서는 해당 WPF 검사를 컴파일만 할 수 있으며 실제 실행으로 보고하지 않는다. `scripts/update-screenshots.ps1`에 새 화면 항목을 포함했다. CI 중단은 계속 유지한다.

### PDF 프로세스와 종료 진단

`Program.Main`은 WPF 시작 전에 `--pdf-worker <parent-pid>`를 구분한다. `PdfBackend`는 앱 폴더의 `wText.exe`로 요청당 한 프로세스를 실행하고 표준 입출력 JSON으로 바이트·결과를 전달한다. 앱의 PDFium 호출은 모두 이 경계를 거치며 저장 전 검사도 `PdfFileService`의 검사 함수로 주입한다. `PdfWorkerClient`는 세 파이프를 동시에 처리하고 메시지 256 MiB, 요청 60초 제한을 적용한다. 취소/실패 후 자식을 종료하고 실제 종료를 기다린다. 부모가 사라지면 작업 프로세스도 종료한다. `PdfWorkerTests`는 실제 PDFium을 사용하는 `WookText.PdfWorker.TestHost`와 강제 종료/잘못된 응답/정지 모드를 검사한다.

PDF 세션 복원은 네이티브 검사보다 버퍼·탭 복구가 먼저다. 첫 Loaded에서 준비하며 실패 후 탭 재선택만으로 자동 재시도하지 않는다. WPF 검사는 손상 PDF와 미저장 텍스트가 함께 복원·편집·재보관되는지, 격리된 미리보기 프로세스를 종료한 뒤 원문과 새로 고침 복구가 유지되는지 확인한다. 미리보기 테스트 프로필은 테스트 설정 폴더 아래에 두며 실제 사용자 브라우저를 종료하지 않는다.

관리 코드의 처리되지 않은 예외는 `%LocalAppData%\wText\logs\startup-errors.log`에 버전·시각·예외를 남기고 1 MiB를 넘으면 `.previous`로 순환한다. 문서 본문을 기록하지 않지만 예외에 파일 경로가 포함될 수 있다. 네이티브 접근 위반·OS 종료는 별도 Windows 이벤트 기록이 필요할 수 있다.

### 설치 소유 목록과 재설치

`package.ps1`은 포함된 Node로 `generate-installer.cjs`를 실행해 `payload-files.nsh`를 만들고 NSIS에 `PAYLOAD_FILES`로 전달한다. 현재 파일과 `legacy-files.txt` 합집합이 제거 대상이고, 현재 배포에 없는 이전 파일만 설치 전에 삭제한다. 새 버전에서 의존성 경로를 제거/변경하기 전에 기존 경로를 누적 목록에 보존해야 한다. 연결 파일·NSIS 변수/따옴표가 포함된 경로는 생성 단계에서 거부한다. `node --test tests/generate-installer.test.cjs`는 Linux에서도 실행할 수 있고 Windows `build.ps1`에도 포함했다.

앱/작업 프로세스는 `Local\wText.Running`, 설치/제거는 `Local\wText.Maintenance` mutex를 유지한다. 설치/제거는 알려진 모든 파일에 쓰기·삭제 권한의 핸들을 열 수 있는지 확인한 뒤 변경한다. 삭제 실패 시 식별자·Uninstall.exe·등록을 남긴다. 사용자 파일만 남으면 `.wtext-retained`로 폴더 소유 정보를 유지한다. 기본 Programs 폴더의 식별자 없는 잔여물은 전체 트리가 알려진 경로이며 연결 경로가 없을 때만 복구한다. 임의 폴더나 사용자 파일은 자동 정리하지 않는다.

[NSIS SetOverwrite](https://nsis.sourceforge.io/Docs/Chapter4.html#setoverwrite)의 `ifdiff`는 파일 시각 비교다. 파일별 zlib 압축을 사용해 건너뛴 파일 때문에 전체 런타임을 풀지 않도록 한다. 다운로드 용량은 거의 유지된다. `test-installer.ps1`은 180초 상한과 실제 소요 시간 기록, 변경 없는 msedge.dll의 쓰기 이벤트 부재, 잠긴 DLL의 설치/제거 거부, 등록 유지, 남긴 사용자 파일 위 재설치, 기본 Programs 잔여물 복구와 사용자 파일 거부를 검사한다. 실제 Windows 실행 없이 속도 개선을 실측 결과로 표기하지 않는다.

## 대용량 문서 개발과 검증 (1.8.0 준비)

`LargeFilePolicy`는 파일/문자/행 한도와 자동 보호 기준을, `TextFileService.ReadChunks`는 엄격한 디코더·청크별 검사·해시를 관리한다. 기존 문자열 API는 작은 입력/비교용으로 유지하며 앱은 `EditorFileLoader`를 사용한다. 로더는 작업 스레드에서 실행 취소를 끈 `TextDocument`에 청크를 넣고 완성 후 소유권을 UI 스레드로 옮긴다. 저장에는 `CreateSnapshot().CreateReader`를 전달하며 스냅샷의 `.Text`를 만들지 않는다. 임시 파일에 전체 인코딩을 마친 뒤에만 해시 확인·원본 교체를 수행한다.

`EditorDocument`는 본문 변경 시 길이/행 개수로 대용량 모드 진입을 판단하고 미리보기·분석·표시 기능을 제한한다. 미리보기는 자체 한도를 넘기면 본문 복사 전에 닫는다. 일반 들여쓰기 감지는 처음 64 KiB 문자 표본으로 제한한다. `MainWindow.LargeFiles.cs`는 창 전체 읽기 예산과 취소 가능한 청크 검색을 담당한다. 대용량 본문은 체크포인트에서 제외하고 세션 형식 3에 `ReopenFromDisk` 경로만 기록한다. 종료 시 큰 미저장 탭을 확인하고 작은 미저장 탭의 기존 보관 동작은 유지한다.

`LargeFileTests`는 약 100 MiB 로그를 직접 생성해 청크 읽기/저장 해시, 인코딩·CRLF·서로게이트 경계, 취소/한도를 검사한다. `ChunkedTextSearchTests`는 청크 경계·역방향·순환·조밀한 일치에서 단일 결과만 찾는지 확인한다. `LargeFileChecks`는 Windows WPF에서 약 100 MiB 파일 열기 중 dispatcher heartbeat, 설정 변경 후 제한 유지, 검색·저장/취소/버리기·재실행과 `large-file.png`를 검사한다. 프로세스 peak working set은 전체 검사 프로세스의 누적값이므로 단일 파일의 메모리라고 해석하지 않는다.

```bash
.tools/dotnet-linux/dotnet test tests/WookText.Core.Tests -c Release -p:RestoreLockedMode=true
.tools/dotnet-linux/dotnet build tests/WookText.App.SmokeTests -c Release -p:EnableWindowsTargeting=true -p:RestoreLockedMode=true
```

위 SDK 경로는 이 Linux 작업 환경에서 확인한 경로다. Windows에서는 앞의 `scripts/build.ps1`을 사용한다. Linux Core 실행·비UI AvalonEdit 버퍼 측정과 실제 Windows WPF 렌더링·입력·메모리 검증을 구분하며 CI를 재개하지 않는다.

### 대용량 스크롤 (1.8.1 준비)

`LargeFileScrolling`은 대용량에서만 PreviewMouseWheel을 처리한다. 16ms Input 우선순위 타이머가 `ScrollWheelBatch`의 마지막 목표 위치를 `IScrollInfo`에 반영한다. 반복 입력으로 타이머를 미루지 않고 각 입력의 범위를 즉시 제한해 끝에서 반전할 때 초과 이동이 누적되지 않는다. fractional delta와 Windows 줄 수/페이지 설정을 따른다. 키보드·클릭·커서 이동·본문 변경·다른 스크롤·숨김/Unload·Dispose·설정 적용 시 타이머/목표를 지운다. Ctrl+휠은 기존 확대 경로에 남긴다.

`EditorDocument.ApplyResourcePreferences`는 대용량에서 `EnableHyperlinks`/`EnableEmailHyperlinks`를 끄고 `ScrollViewer.IsDeferredScrollingEnabled`를 적용한다. 일반 문서로 다시 읽으면 원래 동작으로 복원한다. 의존성은 추가하지 않았다. 고정 [AvalonEdit 링크 생성기](https://github.com/icsharpcode/AvalonEdit/blob/862415d51eddc9eac93f462dbc522ffbf929cd52/ICSharpCode.AvalonEdit/Rendering/LinkElementGenerator.cs)와 [TextView](https://github.com/icsharpcode/AvalonEdit/blob/862415d51eddc9eac93f462dbc522ffbf929cd52/ICSharpCode.AvalonEdit/Rendering/TextView.cs)의 경로를 근거로 했으며 스크롤바는 [WPF 지연 스크롤](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls#deferred-scrolling)을 사용한다. AvalonEdit은 이미 보이는 줄을 가상화하므로 별도의 본문 복사/페이지 뷰를 만들지 않는다.

`ScrollWheelBatchTests`는 이동량·소량 delta·반전·끝 경계·줄/페이지 설정·취소·문서 축소를 확인한다. `LargeFileScrollChecks`는 `LargeFileChecks`의 100 MiB URL/이메일 로그에서 80개 휠 입력의 단일 위치 반영, 키보드/다른 이동/숨김 시 취소, 실제 ScrollViewer의 지연 명령과 놓은 위치, 본문·선택·커서·실행 취소 보존을 검사한다. 일반 문서·편집 중 전환·작은 파일 재로딩도 확인한다. 출력의 elapsed는 테스트 대기 시간을 포함하므로 프레임 시간/개선률로 사용하지 않는다. 실제 Windows 실행 후 고해상도 휠·터치패드·Ctrl+휠·드래그 선택·긴 줄·DPI별 동작을 추가 확인한다.

### 줄 이동 (1.8.2)

`LineNavigation.TryResolve`는 최대 64자 입력에서 줄 또는 줄:열을 검사하며 대상 줄 길이만 콜백으로 조회한다. 전체 본문/행 배열을 요구하지 않는다. `GoToLineWindow`는 공통 테마, 현재 위치/범위, 입력 포커스·기본/취소 버튼과 같은 창의 오류를 제공한다. `MainWindow.Navigation.cs`는 Ctrl+G/편집 메뉴를 연결하고 검색·동시 편집·대기 휠을 취소한 뒤 `GetLineByNumber`로 대상 오프셋을 얻는다. 명령이 Root를 다시 활성화한 뒤 포커스를 돌려주며 활성 문서/버퍼가 바뀌면 이전 요청을 적용하지 않는다. 새 의존성/디렉터리는 없다.

`LineNavigationTests`는 형식/범위·빈 문서·줄 끝·오버플로·과대 붙여넣기와 200만 줄의 단일 행 조회를 검사한다. `GoToLineChecks`는 실제 Ctrl+G 바인딩·메뉴 명령·검색란 포커스·오류/취소·개행/자동 줄바꿈·원문/실행 취소 보존과 `go-to-line.png`를 준비한다. 기존 100 MiB 검사에는 끝/처음 이동을, PDF 검사에는 명령 비활성화를 포함한다. 실제 Windows 실행·새 화면은 Linux 컴파일 결과와 별도로 기록한다.
