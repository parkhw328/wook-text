# 배포와 문서 마무리

## 현재 배포 상태

2026-10-13 기준. 버전의 기준은 `Directory.Build.props`, 공개 여부의 기준은 GitHub Releases다.

| 항목 | 상태 |
| --- | --- |
| 현재 소스·패키지 | **1.4.2**, Ctrl+N·메뉴 새 문서 입력 포커스 수정 |
| 공개 다운로드 | **1.4.1**, 설치 EXE·무설치 ZIP·SHA-256 링크 갱신 |
| 1.4.2 패키지 | 설치 EXE, 런타임 포함 무설치 ZIP, SHA-256 생성·업로드 완료 |
| GitHub 게시 상태 | [Releases](https://github.com/parkhw328/wook-text/releases)의 1.4.2는 권한 있는 사용자만 보는 초안; 공개 최신은 1.4.1 |
| Windows 화면 | 기존 1.3.0 화면 유지. 1.4.0 동시 편집·1.4.1 저장 단축키 화면은 미검증; 생성 코드는 준비 |
| 사용자 확인 | 1.4.1은 사용자 PC 확인 완료; 새 1.4.2 포커스 수정은 확인 대기 |
| 추가 CI | 사용자 요청으로 중단; 재개 지시 전에는 실행하지 않음 |
| 남은 검증 | 1.4.2 Windows WPF 실행·새 화면·IME, 설치·재설치·제거 및 업그레이드 |

1.4.2 초안의 첨부 파일은 다음과 같다. README의 공개 다운로드 링크는 1.4.1을 유지한다.

- `wText-1.4.2-win-x64-setup.exe`
- `wText-1.4.2-win-x64-portable.zip`
- `SHA256SUMS.txt`

로컬 파일은 `artifacts/distribution/wText-1.4.2/`에도 모았다. 생성물은 Git에서 제외하며 초안 릴리스에 보관한다. 빌드 커밋과 SHA-256은 릴리스 설명과 첨부 체크섬에서 확인한다.

## 확인한 결과와 한계

이전 1.4.1의 사용자 확인은 새 포커스 수정의 실행 검증을 의미하지 않는다. 1.4.2는 변경된 앱을 다시 빌드하고 변경 이력을 포함해 패키징했다. Windows 실행 검증과 공개 승격은 남아 있다.

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
