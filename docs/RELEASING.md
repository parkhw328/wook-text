# 배포와 문서 마무리

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
2. **사용 문서:** 바뀐 사용법·단축키·제약을 README와 `docs/REQUIREMENTS.md`에 반영한다. UI 변경은 실제 화면을 확인하고 이미지·설명을 갱신한다.
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
