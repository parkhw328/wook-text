# 배포와 문서 마무리

## 현재 배포 상태

2026-10-13 기준. 버전의 기준은 `Directory.Build.props`, 공개 여부의 기준은 GitHub Releases다.

| 항목 | 상태 |
| --- | --- |
| 현재 소스·패키지 | **1.3.0**, 검색 결과 강조·이전 찾기·바꾸기 전환 |
| 공개 다운로드 | **1.2.4**, README의 공개 링크 유지 |
| 1.3.0 패키지 | 설치 EXE, 런타임 포함 무설치 ZIP, SHA-256 생성·업로드 완료 |
| GitHub 게시 상태 | [Releases 목록](https://github.com/parkhw328/wook-text/releases)의 **wText 1.3.0** 초안; 저장소 쓰기 권한이 있는 로그인 사용자만 접근 가능 |
| Windows 화면 | `docs/images/search-focus.png`에 1.3.0 실제 검색 결과 화면 반영 |
| 추가 CI | 사용자 요청으로 중단; 재개 지시 전에는 실행하지 않음 |
| 남은 검증 | 최종 설치 파일의 Windows 설치·재설치·제거 및 업그레이드 |

초안의 임시 URL은 변경될 수 있으므로 문서에서는 Releases 목록으로 연결한다. 초안의 첨부 파일은 다음과 같다. 공개 전에는 `releases/download/v1.3.0/...`을 일반 사용자 다운로드로 안내하지 않는다.

- `wText-1.3.0-win-x64-setup.exe`
- `wText-1.3.0-win-x64-portable.zip`
- `SHA256SUMS.txt`

로컬 패키지는 `artifacts/installer/`와 `artifacts/`에 있고, 동일 파일을 `artifacts/distribution/wText-1.3.0/`에도 모았다. 생성물은 Git에서 제외하며 릴리스 초안에 보관한다. 패키지 재생성 시 체크섬이 달라지므로 현재 해시는 첨부된 `SHA256SUMS.txt`를 기준으로 한다.

## 확인한 결과와 한계

| 환경·대상 | 결과와 근거 |
| --- | --- |
| Windows Release·단위 테스트 | [첫 Windows 실행](https://github.com/parkhw328/wook-text/actions/runs/38329032895)의 Build and test 단계 통과, 120/120개 |
| Windows WPF·형식 | 같은 실행에서 전체 WPF 검사와 형식 검사 통과; 검색 강조·개수·대소문자·순환 이동·바꾸기·실행 취소·탭 전환 검사 포함 |
| 첫 실행 전체 상태 | 패키징 옵션 전달 오류로 실패. 테스트 단계 통과와 전체 워크플로 성공을 구분함. PowerShell 인수 인용 수정 후 추가 실행은 사용자 요청으로 중단 |
| Linux 단위 테스트 | 114/120개 통과. Windows 경로·숨김 파일·잠금 관련 6개 실패는 변경 전 코드에서도 재현. 검색 관련 18개는 모두 통과 |
| 최종 앱 빌드 | Linux에서 .NET SDK 10.0.401로 Windows x64 Release/self-contained 생성 |
| 설치 파일 생성 | NSIS 3.13 소스의 POSIX 컴파일러와 체크섬을 확인한 동일 버전 공식 배포본 스텁·플러그인 사용 |
| 패키지 무결성 | ZIP CRC 검사, 포함 파일 513개와 publish 결과 일치, 업로드된 EXE·ZIP의 SHA-256 일치 확인 |
| 설치 실행 | 미실행. 생성 성공이나 체크섬 일치를 설치·제거 성공으로 간주하지 않음 |

스크린샷은 위 Windows 실행 결과를 직접 확인한 것이다. 실제 마우스·키보드 장치, 다양한 DPI·IME 환경은 자동 WPF 검사와 별도 범위다. CI 결과 보관 기간이 끝나더라도 검토한 대표 화면은 `docs/images/`에 유지한다.

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
