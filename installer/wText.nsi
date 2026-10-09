Unicode True
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "WinVer.nsh"
!include "FileFunc.nsh"
!include "nsDialogs.nsh"

Var CreateDesktopShortcut
Var DesktopShortcutCheckbox

!ifndef APP_VERSION
  !error "Build with scripts/package.ps1"
!endif
!ifdef SMOKE_INSTALLER
  !define APP_ID "wText-install-smoke"
  !define APP_NAME "wText (installation test)"
!else
  !define APP_ID "wText"
  !define APP_NAME "wText"
!endif
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}"

Name "${APP_NAME}"
OutFile "${OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\${APP_ID}"
InstallDirRegKey HKCU "Software\${APP_ID}" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID zlib
SetOverwrite on
VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey /LANG=1033 "ProductName" "${APP_NAME}"
VIAddVersionKey /LANG=1033 "FileDescription" "${APP_NAME} Windows x64 installer"
VIAddVersionKey /LANG=1033 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Copyright 2026 wText contributors"
VIAddVersionKey /LANG=1033 "CompanyName" "Hyunwook Park"

!define MUI_ABORTWARNING
!define MUI_ICON "..\src\WookText.App\Assets\wText.ico"
!define MUI_UNICON "..\src\WookText.App\Assets\wText.ico"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!define MUI_PAGE_CUSTOMFUNCTION_LEAVE ValidateDirectory
!insertmacro MUI_PAGE_DIRECTORY
Page custom ShortcutPage ShortcutPageLeave
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Korean"
!insertmacro MUI_LANGUAGE "English"

LangString ShortcutTitle ${LANG_KOREAN} "바로가기 설정"
LangString ShortcutSubtitle ${LANG_KOREAN} "wText를 실행할 바로가기를 선택하세요."
LangString ShortcutLabel ${LANG_KOREAN} "바탕화면에 wText 바로가기 만들기(&D)"
LangString ShortcutInfo ${LANG_KOREAN} "현재 사용자의 바탕화면에 아이콘을 만듭니다.$\r$\n체크를 해제하면 기존 wText 바탕화면 바로가기도 제거합니다.$\r$\n시작 메뉴 바로가기는 선택과 관계없이 설치됩니다."
LangString ShortcutTitle ${LANG_ENGLISH} "Shortcut settings"
LangString ShortcutSubtitle ${LANG_ENGLISH} "Choose how to launch wText."
LangString ShortcutLabel ${LANG_ENGLISH} "Create a wText &desktop shortcut"
LangString ShortcutInfo ${LANG_ENGLISH} "Create an icon on the current user's desktop.$\r$\nUncheck to also remove an existing wText desktop shortcut.$\r$\nA Start menu shortcut is always installed."

Function .onInit
  SetShellVarContext current
  SetRegView 64
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "wText requires Windows x64." /SD IDOK
    Abort
  ${EndIf}
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_ICONSTOP "wText requires Windows 10 or newer." /SD IDOK
    Abort
  ${EndIf}
  ; Older installations have no preference: offer the shortcut by default.
  StrCpy $CreateDesktopShortcut ${BST_CHECKED}
  ClearErrors
  ReadRegDWORD $0 HKCU "Software\${APP_ID}" "DesktopShortcut"
  ${IfNot} ${Errors}
  ${AndIf} $0 == 0
    StrCpy $CreateDesktopShortcut ${BST_UNCHECKED}
  ${EndIf}
  ${GetParameters} $0
  ClearErrors
  ${GetOptions} $0 "/DesktopShortcut=" $1
  ${IfNot} ${Errors}
    ${If} $1 == "0"
    ${OrIf} $1 == "1"
      StrCpy $CreateDesktopShortcut $1
    ${Else}
      MessageBox MB_ICONSTOP "Use /DesktopShortcut=0 or /DesktopShortcut=1." /SD IDOK
      SetErrorLevel 2
      Abort
    ${EndIf}
  ${EndIf}
FunctionEnd

Function ShortcutPage
  !insertmacro MUI_HEADER_TEXT "$(ShortcutTitle)" "$(ShortcutSubtitle)"
  nsDialogs::Create 1018
  Pop $0
  ${If} $0 == error
    Abort
  ${EndIf}
  ${NSD_CreateCheckbox} 0 8u 100% 16u "$(ShortcutLabel)"
  Pop $DesktopShortcutCheckbox
  ${NSD_SetState} $DesktopShortcutCheckbox $CreateDesktopShortcut
  ${NSD_CreateLabel} 0 36u 100% 48u "$(ShortcutInfo)"
  Pop $0
  ${NSD_OnBack} ShortcutPageLeave
  nsDialogs::Show
FunctionEnd

Function ShortcutPageLeave
  ${NSD_GetState} $DesktopShortcutCheckbox $CreateDesktopShortcut
FunctionEnd

Function ValidateDirectory
  ${GetRoot} "$INSTDIR" $0
  ${If} "$INSTDIR" == "$0\"
  ${OrIf} "$INSTDIR" == "$0"
    MessageBox MB_ICONSTOP "Choose a dedicated application folder." /SD IDOK
    SetErrorLevel 2
    Abort
  ${EndIf}
  IfFileExists "$INSTDIR\.wtext-install" existing 0
  FindFirst $0 $1 "$INSTDIR\*"
  loop:
    StrCmp $1 "" empty
    StrCmp $1 "." next
    StrCmp $1 ".." next
    FindClose $0
    MessageBox MB_ICONSTOP "The selected folder is not empty. Choose an empty folder for wText." /SD IDOK
    SetErrorLevel 2
    Abort
  next:
    FindNext $0 $1
    Goto loop
  empty:
    FindClose $0
    Return
  existing:
    FileOpen $0 "$INSTDIR\.wtext-install" r
    FileRead $0 $1
    FileClose $0
    StrCmp $1 "${APP_ID}" valid
    MessageBox MB_ICONSTOP "This folder belongs to a different installation." /SD IDOK
    SetErrorLevel 2
    Abort
  valid:
FunctionEnd

Section "wText" MainSection
  Call ValidateDirectory
  SetOutPath "$INSTDIR"
  ClearErrors
  File /r "${PAYLOAD}\*"
  ${If} ${Errors}
    MessageBox MB_ICONSTOP "Installation could not finish. Close wText and retry." /SD IDOK
    SetErrorLevel 3
    Abort
  ${EndIf}
  FileOpen $0 "$INSTDIR\.wtext-install" w
  FileWrite $0 "${APP_ID}"
  FileClose $0
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\${APP_ID}"
  CreateShortcut "$SMPROGRAMS\${APP_ID}\wText.lnk" "$INSTDIR\wText.exe"
  ClearErrors
  ${If} $CreateDesktopShortcut == ${BST_CHECKED}
    CreateShortcut "$DESKTOP\${APP_ID}.lnk" "$INSTDIR\wText.exe" "" "$INSTDIR\wText.exe" 0
  ${Else}
    ${If} ${FileExists} "$DESKTOP\${APP_ID}.lnk"
      Delete "$DESKTOP\${APP_ID}.lnk"
    ${EndIf}
  ${EndIf}
  ${If} ${Errors}
    MessageBox MB_ICONSTOP "Could not update the desktop shortcut. Check desktop access and retry." /SD IDOK
    SetErrorLevel 3
    Abort
  ${EndIf}
  WriteRegStr HKCU "Software\${APP_ID}" "InstallDir" "$INSTDIR"
  WriteRegDWORD HKCU "Software\${APP_ID}" "DesktopShortcut" $CreateDesktopShortcut
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "Hyunwook Park"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "URLInfoAbout" "https://github.com/parkhw328/wook-text"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\wText.exe"
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
SectionEnd

Function un.onInit
  SetShellVarContext current
  SetRegView 64
  FileOpen $0 "$INSTDIR\.wtext-install" r
  FileRead $0 $1
  FileClose $0
  ${If} $1 != "${APP_ID}"
    MessageBox MB_ICONSTOP "Installation marker not found. No files were removed." /SD IDOK
    SetErrorLevel 2
    Abort
  ${EndIf}
FunctionEnd

Section "Uninstall"
  ClearErrors
  !include "${UNINSTALL_FILES}"
  ${If} ${FileExists} "$INSTDIR\wText.exe"
    MessageBox MB_ICONSTOP "Close wText before uninstalling, then run the uninstaller again." /SD IDOK
    SetErrorLevel 3
    Abort
  ${EndIf}
  Delete "$INSTDIR\.wtext-install"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  Delete "$SMPROGRAMS\${APP_ID}\wText.lnk"
  Delete "$DESKTOP\${APP_ID}.lnk"
  RMDir "$SMPROGRAMS\${APP_ID}"
  DeleteRegKey HKCU "${UNINSTALL_KEY}"
  DeleteRegKey HKCU "Software\${APP_ID}"
SectionEnd
