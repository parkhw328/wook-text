# 배포와 문서 마무리

## 1.7.2 공개 배포 요청 (2026-11-02)

사용자가 “일단 현재는 된거같아”라고 확인한 뒤 커밋·푸시·릴리스를 요청했다. 아래 로컬 검토용 구현을 기능 변경 없이 공개하며, 1.7.0~1.7.1의 PDF/미리보기 개발 내용도 첫 공개에 포함한다. 기존 CI 중단을 유지한다.

사용자 확인과 자동 Windows WPF·IME·설치/제거·성능·새 화면 검증을 구분한다. 아래 Linux Core 216개 중 210개 통과(기존 환경 의존 실패 6개), 관련 65개 및 설치 목록 3개 통과와 Windows 대상 컴파일 결과를 기준으로 공개한다. 새 Windows 화면은 미촬영이다.

최종 변경 이력을 포함한 커밋에서 Release 빌드·패키징하고 EXE·ZIP·SHA256SUMS를 첨부한다. 빌드 커밋·태그·최종 크기·다운로드 검증 결과는 공개 완료 후 이 기록과 `artifacts/verification/1.7.2/release/`에 남긴다. 게시 전까지 README의 기존 1.6.2 다운로드 링크를 유지한다.

## 1.7.2 로컬 검토용 준비 (2026-11-02, 미공개)

사용자가 이전 PDF 작업을 복원할 때 로딩 중 종료, 미리보기 활성화 시 종료, Programs 제거 잔여물 때문에 재설치 실패, 재설치 지연을 신고했다. 소스·검토 패키지는 **1.7.2**, 공개 다운로드는 **1.6.2**다. 커밋·푸시·공개 릴리스·CI 재개는 수행하지 않았다.

- 확인한 코드 경로: PDFium을 앱 프로세스에서 호출하던 구조, 미리보기 컨트롤을 오류 처리 밖에서 생성하고 ProcessFailed 오류 표시 중 Stop을 호출하던 경로, 제거 시 wText.exe만 없으면 다른 DLL 삭제 실패에도 식별자/등록을 제거하던 처리, 재설치 때 전체 런타임을 풀고 덮어쓰던 설정을 확인했다. 사용자 PC의 문제 PDF·종료 로그는 없으므로 실제 종료 원인을 확정하거나 해당 PC에서 해결을 재현한 것으로 표시하지 않는다.
- 수정: PDF 작업을 요청당 별도 프로세스로 격리하고 60초 제한·취소·자식 종료 확인을 적용했다. PDF 세션은 버퍼/탭부터 복원하고 실패는 해당 탭에서 안내·재시도한다. 미리보기는 표준 WPF WebView2로 바꾸고 생성·자원 처리·정리 오류를 처리하며 새로 고침으로 브라우저를 복구한다. 관리 코드 종료 기록을 추가했다.
- 설치: 실행 중 앱/유지관리 mutex와 전체 소유 파일의 잠금을 변경 전에 검사한다. 일부 삭제 실패 시 등록/식별자/제거 프로그램 유지, 이전 버전 소유 목록으로 알려진 파일만 정리, 기본 Programs의 알려진 잔여물 복구, 사용자 추가 파일과 작은 소유 확인 파일을 보존한 폴더의 재설치를 지원한다. 파일별 압축과 수정 시각 기준 건너뛰기를 적용했으며 실제 설치 시간은 미측정이다.
- Linux Core: **216개 중 210개 통과, 6개 실패**. 이전 1.7.1 TRX와 실패 이름 6개가 정확히 일치한다(Windows 경로·숨김 속성·파일 잠금). PDF·미리보기·설명서 관련 **65개 전부 통과**이며 이 중 새 작업 프로세스 검사 6개는 실제 네이티브 PDF 읽기/렌더링/검색/편집, 비정상 종료·잘못된 응답·손상 PDF·시간 초과·취소를 확인했다. 설치 소유 목록 Node 검사 **3개 통과**.
- Windows 대상: 잠금 복원 및 앱·WPF 검사·작업 프로세스 테스트 호스트의 Release 컴파일 **경고·오류 0개**. WPF 검사에 손상 PDF 혼합 세션의 내용 보존·다른 문서 편집/보관, 미리보기 프로세스 종료 후 창/원문 유지·새로 고침 복구를 추가했다. 설치 검사에는 잠긴 DLL, 실행 mutex, Programs 잔여물, 사용자 파일 보존/재설치, 변경 없는 대형 DLL의 쓰기 이벤트 부재와 소요 시간 기록을 추가했다. 이 Windows 검사는 아직 실행하지 않았다.
- 문서: README·FEATURES·내장 USER-GUIDE·DEVELOPMENT·규칙·요구사항·CHANGELOG를 갱신했다. 새 Windows 화면은 미촬영이다. 표준 WebView2의 HWND 화면은 WPF RenderTargetBitmap만으로 캡처할 수 없으므로 Windows 실제 화면 캡처가 필요하다.
- 패키지: `artifacts/distribution/wText-1.7.2/`의 설치 EXE **436,911,154 bytes**·무설치 ZIP **438,414,758 bytes**·SHA256SUMS. ZIP의 **802개 파일**이 publish와 일치하고 CRC·SHA-256을 확인했다. 누적 설치 소유 목록은 804개로 이전 1.6.x의 PDB 2개도 정리한다. 앱/설치 파일 버전 1.7.2.0, 내장 설명서·라이선스·고정 런타임 원본 일치를 확인했다. PDF 분기용 Program.Main 진입점과 앱의 직접 PdfEngine 참조 부재도 확인했다.
- 근거: `artifacts/verification/1.7.2/`의 테스트 TRX·빌드/패키징 로그·metadata-check·source-manifest.json·package-verification.json. Linux 교차 게시·NSIS 컴파일을 실제 설치 실행과 구분한다. 빌드 소스는 커밋 전 작업 트리다.

남은 Windows 검증: 사용자 PDF의 이전 세션을 유지한 업그레이드·시작, 실제 미리보기 켜기/동의/브라우저 종료 복구, 표준 WebView2의 메뉴·탭·포커스·화면, 설치/재설치/제거와 잠긴 파일·잔여물 복구 및 소요 시간, 실제 새 화면. 기존 PDF 지원 한계와 CI 중단은 유지한다.

## 1.7.1 로컬 검토용 준비 (2026-11-02, 미공개)

사용자가 PDF 글꼴 무시·상단 도구 정리·다양한 작성 형식 미리보기를 요청하고 ‘수정한 글자만 기본 글꼴’, ‘JavaScript 없이 레이아웃 미리보기’를 선택했다. 당시 소스·검토 패키지는 **1.7.1**이었으며 이후 수정은 위 1.7.2 기록을 따른다. 공개 다운로드는 **1.6.2**다. 커밋·푸시·공개 릴리스·CI 재개는 수행하지 않았다.

- 구현: PDF의 수정한 텍스트 개체에만 기본 글꼴 적용·설정 유지, 주석/편집/페이지 메뉴와 필요할 때만 나타나는 검색/개체 도구. Markdown·HTML·SVG·CSV/TSV·JSON·XML 미리보기, 자동/수동 선택·원문 실시간 반영·형식 복원.
- 런타임: WebView2 SDK 1.0.4314.35, Fixed Runtime 156.0.4314.58 x64의 원본 CAB 전체를 앱에 포함했다. 별도 설치가 필요 없으며 압축 전 약 675 MiB가 추가된다. 원문 JavaScript·원격 리소스는 차단하고 문서 폴더 안의 CSS·이미지·글꼴만 반환한다. 런타임 약관·SmartScreen 고지·최초 사용 동의, 포함 구성요소 고지 열기를 추가했다.
- Linux Core: **210개 중 204개 통과, 6개 실패**. 앞선 1.7.0 작업에서 변경 전 HEAD에서도 확인한 Windows 경로·숨김 속성·잠금 관련 동일한 6개 실패다. PDF·미리보기·설명서 관련 **59개 검사 전부 통과**. 로그와 TRX를 보관한다.
- Windows 대상: 앱·WPF 검사 프로젝트의 잠금 복원 및 Release 컴파일 **경고·오류 0개**. 실제 Windows 실행 결과가 아니다.
- 정적 렌더링: Linux Chromium 153.0.8010.12에서 Markdown 표·HTML의 로컬 CSS grid/이미지·스크립트 비활성화·외부 이미지/프레임 차단·CSV·JSON 접기·XML·SVG의 6개 예제를 확인했다. 해당 결과와 PNG는 `linux-browser-check.json`, `linux-preview-*.png`에 보관한다. 이는 Windows WebView2/WPF 화면 검증을 대신하지 않는다.
- 문서: README·FEATURES·내장 USER-GUIDE·DEVELOPMENT·규칙·요구사항·CHANGELOG·라이선스 안내를 갱신했다. 새 Windows 화면은 만들지 못했으므로 기존 문서 이미지를 새 버전 화면으로 표시하지 않는다.
- 패키지 위치: `artifacts/distribution/wText-1.7.1/`의 설치 EXE·무설치 ZIP·SHA256SUMS. 설치 EXE **435,878,164 bytes**, ZIP **438,403,309 bytes**. ZIP의 **802개 파일**이 publish 결과와 일치하고 CRC·SHA-256을 확인했다. WebView2 원본 추출본의 **257개 파일(706,209,931 bytes)**이 모두 포함되고 PDFium DLL·WebView2Loader가 고정 NuGet 원본과 일치한다. 라이선스·CHANGELOG·내장 설명서도 원문과 대조했다.
- 소스 해시·로그·패키지 검증은 `artifacts/verification/1.7.1/`의 `source-manifest.json`·`package-verification.json`과 로그에 보관한다. 빌드 소스는 커밋 전 작업 트리이며 GitHub 첨부 파일은 만들지 않았다.

남은 Windows 검증: 최소/기본 창의 메뉴·키보드·PDF 선택/수정·IME·저장, WebView2 초기 동의·Windows 10 ACL·탭/포커스·복사·스크롤·로컬 리소스/오류·브라우저 종료 복구, PowerShell CAB 준비, 설치/업그레이드/제거 및 실제 스크린샷. 아래 1.7.0의 PDF 지원 한계도 유지한다.

## 1.7.0 로컬 검토용 준비 (2026-11-02, 미공개)

사용자가 PDF 읽기·검색·주석·페이지 조작·기존 글자/이미지 편집과 Markdown 미리보기 구현을 요청했다. 당시 준비 소스 버전은 **1.7.0**이었다. 이후 변경은 위 1.7.1 기록을 따른다. 공개 다운로드는 **1.6.2**를 유지한다. 공개 릴리스, 커밋·푸시, CI 재개는 수행하지 않았다. 이 작업은 초기 지원 범위를 구현한 검토용 상태이며 Windows 실사용 검증 완료를 뜻하지 않는다.

- 구현: PDFium 157.0.8100 기반 전용 PDF 탭, 텍스트 선택·복사·전체 검색, 형광펜·메모·사각형·한 줄 텍스트, 페이지 회전·삭제·이동·병합, 최상위 글자/이미지 개체 수정·삭제. 저장 충돌 차단·실행 취소·외부 변경 감지·PDF/텍스트 혼합 세션. Markdig 1.5.1 기반 나란히 Markdown 미리보기와 Ctrl+Shift+M.
- 지원 한계: PDF의 그룹 내부 개체·자동 문단 재배치·OCR·암호 입력은 미지원. 서명 PDF는 읽기 전용. 긴 글자 교체 시 겹침 가능, 대체 글꼴과 객체 쌓임 순서 변경 가능. 병합은 페이지 단위이며 소스 책갈피/문서 수준 양식은 병합하지 않는다. 자세한 한도와 조작은 F1 설명서와 기능 안내에 기록했다.
- Linux Core: **188개 중 182개 통과, 6개 실패**. 변경 전 HEAD를 별도 임시 디렉터리에 풀어 **165개 중 159개 통과, 동일한 6개 실패**를 재현했다. 실패는 Windows 경로·숨김 속성·파일 잠금과 관련된 기존 환경 차이다. 새 PDF/Markdown 검사 21개와 설명서 검색 검사 2개는 모두 통과했다. PDF 엔진의 실제 Linux 네이티브 실행 결과이며 단순 모의 호출이 아니다.
- Windows 대상: 앱·WPF 검사 프로젝트 Release 컴파일을 확인했다. 실제 WPF·클립보드·IME·고해상도·설치 실행은 Linux 환경에서 미실행이다. `PdfPreviewChecks`는 PDF 열기→검색·복사→회전·실행 취소→저장→혼합 세션 복원, Markdown 미리보기·원문 유지와 화면 생성을 준비했다.
- 화면: 새 실제 화면은 미촬영. Windows 검사 후 `pdf-reader.png`, `pdf-reader-minimum.png`, `markdown-preview.png`를 검토해 반영해야 한다. 기존 Windows 이미지를 새 기능의 검증 근거로 쓰지 않는다.
- 문서·라이선스: README의 준비 버전 안내와 기존 다운로드, FEATURES·USER-GUIDE·DEVELOPMENT·규칙·요구사항·CHANGELOG를 동기화했다. Windows 엔진과 Markdig, PDFium 배포본의 모든 구성요소 라이선스를 앱 도움말·패키지에 포함한다.
- 로컬 패키지 위치: `artifacts/distribution/wText-1.7.0/`의 설치 EXE·무설치 ZIP·`SHA256SUMS.txt`. Linux에서 교차 게시와 NSIS 컴파일을 수행하며 Windows 설치 성공으로 해석하지 않는다. ZIP의 535개 파일과 publish 내용이 일치하고 CRC, 고정 NuGet 패키지의 PDFium DLL 원본 일치, 내장 설명서·고지·버전·체크섬을 검증했다. 설치 EXE 125,153,741 bytes, 무설치 ZIP 126,637,193 bytes. 결과는 `package-verification.json`에 기록했다.
- 빌드 소스: 커밋되지 않은 작업 트리다. 기반 HEAD와 소스 파일 해시는 `artifacts/verification/1.7.0/source-manifest.json`, 테스트·빌드·패키징 로그는 같은 폴더에 보관한다. GitHub 릴리스나 첨부 파일은 만들지 않았다.

남은 Windows 검증: PDF 마우스 선택 방향·회전/CropBox·IME 글자 교체·메모 클릭·이미지 교체·대형 문서 응답성, 원본 변경/삭제·저장 실패·닫기/재실행, Markdown 표/로컬 이미지/스크롤/링크·최소 창, 설치·업그레이드·제거. 공개 전에 실제 화면과 검증 결과를 이 기록에 추가한다.

## 1.6.2 공개 배포 상태 (2026-10-30)

사용자 요청에 따라 짧은 문서 미니맵 수정의 커밋·푸시·공개 릴리스를 완료했다. 소스와 공개 다운로드는 **1.6.2**이며 [wText 1.6.2](https://github.com/parkhw328/wook-text/releases/tag/v1.6.2)을 최신 정식 릴리스로 게시했다.

- 행당 최대 2px, 위쪽 배치, 실제 문서 높이 내 화면/커서 표시, 그리기와 클릭 좌표 통일, 빈 문서 위치 표시 생략.
- Core 미니맵 좌표 회귀 검사 13개 통과. Windows 대상 앱·WPF 검사 프로젝트 Release 빌드 확인. Windows 실제 화면·입력·설치 실행은 Linux 환경에서 미실행이다. 기존 CI 중단 유지.
- `minimap-short.png` 생성 검사를 추가했으며 기존 화면을 새 검증으로 표시하지 않는다. F1 설명서·기능 안내·요구사항·변경 이력을 갱신했다.
- 로컬 패키지: `artifacts/distribution/wText-1.6.2/`에 설치 EXE·무설치 ZIP·SHA256SUMS.txt를 생성했다. ZIP CRC와 515개 publish 파일 일치, 내장 설명서·변경 이력과 로컬 체크섬을 확인했다. 검증 로그는 `artifacts/verification/1.6.2/`에 보관했다. Linux 교차 게시·NSIS 생성과 Windows 실행 검증을 구분한다. 소스·바이너리 빌드 커밋 및 태그는 `b28e514677850d7f8f5cbbc9acbd6131d631a7b7`이다. 공개 확인 후 README·배포 기록만 별도 문서 커밋으로 갱신했다.
- 설치 EXE(121,220,768 bytes)·ZIP(122,688,238 bytes)·체크섬 파일을 GitHub에서 다시 내려받아 로컬 배포본과 SHA-256 및 체크섬 원문 일치를 확인했다. `artifacts/verification/1.6.2/upload-verification.json`에 결과를 보관했다.

## 1.6.1 공개 배포 상태 (2026-10-23)

사용자 요청에 따라 블록 선택 변환 단축키 Ctrl+Alt+B를 추가하고 커밋·푸시·공개 릴리스를 완료했다. 소스와 공개 다운로드는 **1.6.1**이며 [wText 1.6.1](https://github.com/parkhw328/wook-text/releases/tag/v1.6.1)을 최신 정식 릴리스로 게시했다. F1 설명서·기능 안내·요구사항·변경 이력을 갱신했다.

- 기존 Ctrl+B·블록 복사 Ctrl+Shift+C·Alt+드래그·Alt+Shift+방향키를 유지한다. 본문에만 새 단축키를 연결했다.
- 검증: 설명서 검색과 WPF 단축키→블록 변환→복사 경로·원문/실행 취소 보존 검사를 갱신했다. Linux Core 테스트 152개 중 146개 통과(설명서 14개 전부 통과); 실패 6개는 기존 환경 의존 실패와 일치한다. 앱·WPF 검사 프로젝트 Windows 대상 Release 빌드 경고·오류 0개. WPF 검사는 실제 실행과 구분한다.
- Windows 실제 단축키·화면 캡처·설치/업그레이드/제거는 환경상 미실행이다. 기존 CI 중단을 유지하며 릴리스 설명에 이 한계를 표시한다. 이번에는 메뉴의 단축키 표시만 바뀌며 기존 화면을 새 검증 자료로 표시하지 않는다.
- 소스·바이너리 빌드 커밋 및 태그: `a92419f636eef11627099dcba3635498d40ca142`. 공개 확인 후 README·배포 기록만 별도 문서 커밋으로 갱신했다.
- 설치 EXE·무설치 ZIP·SHA256SUMS.txt를 `artifacts/distribution/wText-1.6.1/`와 GitHub에 제공한다. EXE 121,220,705 bytes, ZIP 122,687,046 bytes. ZIP CRC와 515개 publish 파일 일치, 내장 설명서 원문·CHANGELOG·바이너리 소스 커밋을 확인했다.
- 업로드 파일 3개를 다시 내려받아 체크섬 파일 원문과 EXE·ZIP SHA-256이 로컬 배포본과 일치함을 확인했다. 로그는 `artifacts/verification/1.6.1/`에 보관했다.

## 1.6.0 공개 배포 상태 (2026-10-23)

사용자 요청에 따라 F1 기능 설명서 추가와 점검 후 커밋·푸시·공개 릴리스를 완료했다. 소스와 공개 다운로드는 **1.6.0**이며 [wText 1.6.0](https://github.com/parkhw328/wook-text/releases/tag/v1.6.0)을 최신 정식 릴리스로 게시했다. 기존 CI 중단은 유지했다.

- 기능: 미니맵·표시 설정, 직사각형 블록 클립보드, 줄 복제·이동·삭제, 도움말 메뉴와 F1의 내장 설명서. 설명서는 16개 주제와 제목·본문 검색, Ctrl+F·Esc, 창 재사용을 제공한다. 새 외부 의존성 없음.
- Linux Core 테스트: 151개 중 145개 통과. 새 설명서 리소스·기능별 검색·복수 검색어 검사 13개 전부 통과. 6개 실패는 기존 Windows 자연 정렬 DLL·경로·숨김 속성·파일 잠금 항목이며 변경 전 `3ae03ed` 소스를 별도 디렉터리에서 실행해 같은 6개 실패를 재현했다.
- 앱 및 WPF 검사 프로젝트: .NET SDK 10.0.401, Windows x64 Release 빌드 경고·오류 0개. 새 C# 파일 whitespace formatter 확인. 블록/줄 편집·미니맵·도움말 WPF 회귀 검사는 작성·컴파일했으나 Linux에서 실행하지 않았다.
- 문서: README, CHANGELOG, 기능 안내, 요구사항, 개발 안내, 내장 `USER-GUIDE.md`와 규칙을 동기화. 이전의 미니맵 미지원 문구를 수정했다.
- 화면: 실제 1.6.0 화면은 미촬영. `minimap.png`·`user-guide.png` 생성 검사와 `update-screenshots.ps1` 목록을 준비했다. 기존 Windows 화면을 새 버전 검증으로 사용하지 않는다.
- 소스·바이너리 빌드 커밋과 릴리스 태그: `8949313baab34de68a26e20651be18f1c1b594a9` (Add minimap, block editing, and offline F1 manual). README 다운로드와 이 배포 기록은 공개 확인 후 별도 문서 커밋으로 갱신했다. 패키지에 포함되는 코드·내장 설명서·CHANGELOG는 빌드 커밋과 같다.
- 패키지: Linux 교차 게시·NSIS 생성 완료. `artifacts/distribution/wText-1.6.0/` 및 GitHub 릴리스에 `wText-1.6.0-win-x64-setup.exe` (121,220,207 bytes), `wText-1.6.0-win-x64-portable.zip` (122,686,696 bytes), `SHA256SUMS.txt` 제공.
- 무결성: ZIP CRC와 515개 publish 파일 일치, 내장 설명서 원문·CHANGELOG·바이너리 소스 커밋 확인. GitHub 첨부 3개를 다시 내려받아 체크섬 파일 원문과 EXE·ZIP SHA-256을 로컬 배포본과 대조했다. 로그와 업로드 확인 기록은 `artifacts/verification/1.6.0/`에 보관했다.
- 남은 Windows 검증: 실제 F1·설정/비교 대화상자의 도움말·검색·닫기·키보드 포커스, Alt+드래그/열 선택·외부 클립보드·한글/IME·DPI·자동 줄바꿈·실제 미니맵/설명서 화면, `scripts/build.ps1` 및 설치·업그레이드·제거. 이 한계를 릴리스에 명시한다.

로컬 검증 명령(.NET SDK 경로는 환경에 맞게 지정):

```bash
dotnet test tests/WookText.Core.Tests -c Release --no-restore
dotnet build tests/WookText.App.SmokeTests -c Release -p:EnableWindowsTargeting=true --no-restore
dotnet publish src/WookText.App -c Release -r win-x64 --self-contained true -p:EnableWindowsTargeting=true -o artifacts/publish/1.6.0-win-x64
git diff --check
```

Windows 실행 검증·화면 캡처 전에는 새 기능을 자동 실사용 검증 완료로 표시하지 않는다.

## 1.5.0 공개 배포 상태 (2026-10-22)

사용자가 1.5.0을 확인한 뒤 커밋·푸시·공개 릴리스를 요청했다. 소스와 공개 다운로드는 **1.5.0**이며 [wText 1.5.0](https://github.com/parkhw328/wook-text/releases/tag/v1.5.0)에 설치 EXE·무설치 ZIP·SHA-256을 제공한다. 추가 CI 중단을 유지한다.

- 앱 소스 커밋: `013c889a56f6333f7720828e23387b1ed0f3e510`. 사용자가 확인한 바이너리를 유지하고 공개 CHANGELOG를 반영해 다시 패키징했다. 공개 태그에는 README·배포 기록 정리도 포함한다.
- Linux: 파일 읽기·저장·인코딩·충돌 Core 테스트 18/18 통과. 앱과 WPF 회귀 프로젝트 Windows 대상 Release 컴파일 경고·오류 0개.
- WPF 회귀 검사: 자동 갱신, 인코딩·언어·커서 유지, 충돌 유지/취소/폐기, 반복 알림 억제, 원본 덮어쓰기 차단, 자체 저장, 삭제·재생성, 수동 갱신, 비활성 탭 보호를 추가했다. 컴파일 검증과 실제 실행을 구분한다.
- 패키지: `artifacts/distribution/wText-1.5.0/`에 설치 EXE·런타임 포함 무설치 ZIP·`SHA256SUMS.txt` 생성 완료. ZIP CRC와 513개 파일의 publish 일치, 배포 폴더 SHA-256을 확인했다. Linux 교차 게시와 NSIS 컴파일이며 Windows 설치 성공을 뜻하지 않는다.
- 문서: README 소개, 기능 안내, 요구사항 수용 기준, CHANGELOG를 갱신했다. 새 알림 화면은 미촬영이며 기존 Windows 화면을 새 버전 검증으로 간주하지 않는다.
- 남은 Windows 검증: `scripts/build.ps1`의 WPF 검사, 실제 편집 중 외부 저장·잠금·삭제·재생성 및 알림 화면, `scripts/test-installer.ps1`의 설치·제거. 향후 Windows 검증과 화면 촬영을 수행한 뒤 기능 안내에 반영한다. 사용자 확인을 자동 WPF·설치/제거 검증 완료로 확대하지 않는다.

릴리스 설명에는 자동 갱신, 미저장 충돌 선택, 수동 다시 불러오기, 삭제 시 버퍼 유지와 검증 범위를 기재했다. 다운로드 파일은 `wText-1.5.0-win-x64-setup.exe`, `wText-1.5.0-win-x64-portable.zip`, `SHA256SUMS.txt`이며 로컬 배포 폴더와 GitHub 첨부 파일의 SHA-256을 대조한다.

## 1.4.2 공개 배포 기록

2026-10-13 기준. 버전의 기준은 `Directory.Build.props`, 공개 여부의 기준은 GitHub Releases다.

| 항목 | 상태 |
| --- | --- |
| 현재 소스·패키지 | **1.4.2**, Ctrl+N·메뉴 새 문서 입력 포커스 수정 |
| 공개 다운로드 | **1.4.2**, 설치 EXE·무설치 ZIP·SHA-256 링크 갱신 |
| 1.4.2 패키지 | 설치 EXE, 런타임 포함 무설치 ZIP, SHA-256 생성·업로드 완료 |
| GitHub 게시 상태 | [wText 1.4.2](https://github.com/parkhw328/wook-text/releases/tag/v1.4.2) 공개·최신 릴리스 |
| Windows 화면 | 기존 1.3.0 화면 유지. 1.4.0 동시 편집·1.4.1 저장 단축키 화면은 미검증; 생성 코드는 준비 |
| 사용자 확인 | 사용자 PC에서 1.4.2 포커스 수정 확인 후 공개 요청 |
| 추가 CI | 사용자 요청으로 중단; 재개 지시 전에는 실행하지 않음 |
| 남은 검증 | 1.4.2 자동 Windows WPF 실행·새 화면·IME, 설치·재설치·제거 및 업그레이드 |

1.4.2 공개 릴리스의 첨부 파일은 다음과 같다. README의 공개 다운로드 링크도 1.4.2로 갱신했다.

- `wText-1.4.2-win-x64-setup.exe`
- `wText-1.4.2-win-x64-portable.zip`
- `SHA256SUMS.txt`

로컬 파일은 `artifacts/distribution/wText-1.4.2/`에도 모았다. 생성물은 Git에서 제외하며 공개 릴리스에 보관한다. 빌드 커밋과 SHA-256은 릴리스 설명과 첨부 체크섬에서 확인한다.

## 확인한 결과와 한계

사용자가 PC에서 1.4.2 포커스 수정 동작을 확인하고 공개를 요청했다. 확인된 앱 바이너리를 유지하고 공개 상태의 CHANGELOG만 반영해 다시 패키징했다. 앱 빌드 커밋은 `325bcc69f50c833d57718c655943433e6a502926`이며 공개 태그에는 문서 정리도 포함한다. 사용자 확인을 자동 WPF·IME·설치/제거 시험 완료로 확대하지 않는다.

| 환경·대상 | 결과와 근거 |
| --- | --- |
| Linux Core 테스트 | 1.4.0에서 138개 중 132개 통과. 동시 편집 18개와 검색 18개 모두 통과. 1.4.1·1.4.2는 Core 변경 없음 |
| 기존 환경 의존 실패 | Windows 경로·숨김 파일·잠금 관련 6개. 앞선 변경 전 코드에서도 재현한 실패이며 이번 작업에서 해당 코드는 변경하지 않음 |
| Windows 대상 컴파일 | 앱 및 WPF 검사 프로젝트 Release 빌드 경고·오류 0개 |
| WPF 회귀 검사 | 1.4.2: 새 문서 단축키/메뉴·검색창/편집기 포커스·입력·붙여넣기·비활성 탭 보호 검사 추가. 1.4.1: 저장창 Y/N/C·실제 종료 저장/버리기/취소·오류창 미적용 검사 추가. 1.4.0: 연속 입력, 붙여넣기, 잘라내기, Unicode 삭제, 인접한 빈 대상, 실행 취소/다시 실행, 모드 해제 검사 추가. 컴파일만 확인, 실행은 미검증 |
| 새 화면·실제 입력 | `new-document-focus.png`와 `search-linked-editing.png` 생성 코드 준비. Windows WPF·물리 키보드·한글 IME·DPI 검증 미실행 |
| 패키지 생성 | Linux .NET SDK 10.0.401 → Windows x64 self-contained. NSIS 3.13 POSIX 컴파일러와 동일 버전 공식 배포본 스텁·플러그인 사용 |
| 패키지 무결성 | ZIP CRC 및 513개 포함 파일과 publish 결과 일치, 업로드 파일 SHA-256 대조 |
| 설치 실행 | 자동 검증 미실행. 패키지 생성·체크섬 일치를 Windows 설치·제거 성공으로 간주하지 않음 |

**이전 1.3.0 검증과 구분:** [첫 Windows 실행](https://github.com/parkhw328/wook-text/actions/runs/38329032895)의 Build and test 단계에서 당시 단위 테스트 120개와 전체 WPF 검사가 통과했다. 해당 실행 전체는 패키징 옵션 전달 오류로 실패했고 PowerShell 인수 인용을 수정했다. 이후 추가 CI는 사용자 요청으로 중단했다. 이 결과를 새 동시 편집 기능의 Windows 검증으로 재사용하지 않는다.

`docs/images/search-focus.png`는 위 1.3.0 Windows 실행에서 직접 확인한 실제 화면이다. 새 체크박스가 보이지 않는 이유를 README에도 표시한다. 저장 확인창의 기존 이미지에도 1.4.1 단축키 표시는 없다. Windows 실행을 재개할 수 있을 때 새 WPF 검사와 `update-screenshots.ps1`을 실행하고 `search-linked-editing.png`와 `save-confirmation.png`를 검토한 뒤 사용 설명에 추가한다.

## 환경에 관계없이 적용할 완료 순서

1. **기능과 버전:** `Directory.Build.props`와 해당 CHANGELOG 항목을 맞춘다. README 첫 소개에 현재 소스·패키지 버전과 공개 버전을 각각 표시한다.
2. **사용 문서:** 바뀐 사용법·단축키·제약을 README와 `rules/requirements.md`에 반영한다. UI 변경은 실제 화면을 확인하고 이미지·설명을 갱신한다.
3. **검증 기록:** 실행한 환경·커밋·명령·결과와 미실행 이유를 기록한다. 환경 차이로 실패했다면 변경 전 결과와 대조한다. CI 중단 요청은 계속 지킨다.
4. **최종 패키지:** 패키지에 포함되는 CHANGELOG·라이선스·코드 수정이 끝난 커밋에서 생성한다. 문서만 수정했어도 포함 파일이 달라졌다면 패키지를 다시 만든다.
5. **첨부 파일:** ZIP 내용을 검사하고 EXE·ZIP 체크섬을 생성한다. 동일 파일명으로 초안을 갱신했다면 모든 첨부 파일의 크기·SHA-256을 다시 대조하고 빌드 커밋을 릴리스 설명에 기록한다.
6. **게시 상태:** 초안이면 README의 기존 공개 링크를 유지한다. 공개 게시 요청에 따라 게시했을 때만 README의 다운로드 버전·세 링크(EXE/ZIP/체크섬)·소개, CHANGELOG와 이 문서의 상태를 함께 갱신한다.
7. **전달:** 관련 변경을 커밋·푸시하고 작업 트리, 원격 커밋, 릴리스 상태를 확인한다. 최종 안내에 패키지 위치와 남은 검증을 명시한다.

## 환경별 실행 범위

Windows에서는 README의 `bootstrap.ps1`, `build.ps1`, `package.ps1 -SmokeInstaller`, `test-installer.ps1`, `package.ps1`을 순서대로 사용한다. 화면이 바뀌면 `update-screenshots.ps1` 결과를 검토한다. 검사와 패키징은 같은 `obj`를 사용하므로 동시에 실행하지 않는다.

Linux에서는 문서·소스·버전·릴리스 설명·체크섬 정리를 그대로 수행한다. `global.json`에 지정된 SDK로 Core 테스트를 실행할 수 있으며 Windows 대상 컴파일에는 `-p:EnableWindowsTargeting=true`가 필요하다. 앱을 빌드하려면 기존 스크립트와 같은 버전·체크섬의 Windows Node.js와 Prettier도 준비해야 한다. `package.ps1`은 Windows용이며 Linux에서 그대로 실행하는 명령으로 안내하지 않는다.

1.3.0의 Linux 패키징은 위 검증된 도구를 별도로 준비한 수동 작업이었다. NSIS 컴파일 방식은 [공식 POSIX 빌드 안내](https://nsis.sourceforge.io/Docs/AppendixG.html)를 따른다. Windows용 런타임의 라이선스·고지와 파일별 제거 목록은 `scripts/package.ps1`과 동일하게 포함한다. Linux 컴파일 성공을 WPF 실행·설치 검증으로 대체하지 않는다.

문서 수정 뒤에는 `git diff --check`와 상대 링크·이미지 경로·버전·파일명을 확인한다. 문서 작업 때문에 CI를 다시 실행할 필요는 없다.
