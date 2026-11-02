Unicode True
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "WinVer.nsh"
!include "FileFunc.nsh"
!include "nsDialogs.nsh"

Var CreateDesktopShortcut
Var DesktopShortcutCheckbox
Var MaintenanceMutex
Var Candidate
Var KnownPath
Var ScanRelative
Var OrphanValid
Var FileProblem
Var ProblemPath

!include "${PAYLOAD_FILES}"

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
; Independent compressed files let an unchanged runtime be skipped without expanding the full archive.
SetCompressor zlib
SetOverwrite ifdiff
AllowSkipFiles off
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
  Call BeginMaintenance
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
  System::Call 'kernel32::GetFileAttributesW(w "$INSTDIR") i.r0'
  IntOp $0 $0 & 0x400
  ${If} $0 != 0
  ${AndIf} ${FileExists} "$INSTDIR\*"
    MessageBox MB_ICONSTOP "The application folder must not be a link." /SD IDOK
    SetErrorLevel 2
    Abort
  ${EndIf}
  IfFileExists "$INSTDIR\.wtext-install" existing 0
  IfFileExists "$INSTDIR\.wtext-retained" retained 0
  FindFirst $0 $1 "$INSTDIR\*"
  loop:
    StrCmp $1 "" empty
    StrCmp $1 "." next
    StrCmp $1 ".." next
    FindClose $0
    ; Recover an incomplete old uninstall only in the product's default folder,
    ; and only if every remaining path is in the packaged ownership catalog.
    StrCmp "$INSTDIR" "$LOCALAPPDATA\Programs\${APP_ID}" 0 occupied
    StrCpy $OrphanValid 1
    StrCpy $ScanRelative ""
    Call CheckOrphanTree
    StrCmp $OrphanValid 1 valid occupied
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
  retained:
    FileOpen $0 "$INSTDIR\.wtext-retained" r
    FileRead $0 $1
    FileClose $0
    StrCmp $1 "${APP_ID}" valid occupied
  occupied:
    MessageBox MB_ICONSTOP "The selected folder contains files that are not recognized as a wText installation. Choose a dedicated application folder." /SD IDOK
    SetErrorLevel 2
    Abort
  valid:
FunctionEnd

Function CheckOrphanTree
  Push $0
  Push $1
  Push $2
  Push $3
  FindFirst $0 $1 "$INSTDIR\$ScanRelative*"
  loop:
    StrCmp $1 "" done
    StrCmp $1 "." next
    StrCmp $1 ".." next
    StrCpy $Candidate "$ScanRelative$1"
    Call IsKnownPath
    StrCmp $KnownPath 1 0 invalid
    System::Call 'kernel32::GetFileAttributesW(w "$INSTDIR\$Candidate") i.r2'
    IntOp $3 $2 & 0x400
    IntCmp $3 0 +2
      Goto invalid
    IntOp $2 $2 & 0x10
    IntCmp $2 0 next
    Push $ScanRelative
    StrCpy $ScanRelative "$Candidate\"
    Call CheckOrphanTree
    Pop $ScanRelative
    StrCmp $OrphanValid 1 next done
  invalid:
    StrCpy $OrphanValid 0
    Goto done
  next:
    FindNext $0 $1
    Goto loop
  done:
    FindClose $0
    Pop $3
    Pop $2
    Pop $1
    Pop $0
FunctionEnd

!macro MaintenanceFunctions Prefix
Function ${Prefix}BeginMaintenance
  System::Call 'kernel32::CreateMutexW(p 0, i 0, w "Local\wText.Maintenance") p.r0 ?e'
  Pop $1
  StrCpy $MaintenanceMutex $0
  ${If} $0 == 0
  ${OrIf} $1 == 183
    MessageBox MB_ICONSTOP "Another wText installation or removal is in progress." /SD IDOK
    SetErrorLevel 3
    Abort
  ${EndIf}
  System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\wText.Running") p.r0'
  ${If} $0 != 0
    System::Call 'kernel32::CloseHandle(p r0)'
    MessageBox MB_ICONSTOP "Close wText and wait for its PDF/preview processes to exit, then retry. No files were changed." /SD IDOK
    SetErrorLevel 3
    Abort
  ${EndIf}
FunctionEnd
!macroend
!insertmacro MaintenanceFunctions ""
!insertmacro MaintenanceFunctions "un."

!macro CheckDirectory Relative
  System::Call 'kernel32::GetFileAttributesW(w "$INSTDIR\${Relative}") i.r0'
  ${If} $0 != -1
    IntOp $0 $0 & 0x400
    ${If} $0 != 0
      StrCpy $FileProblem 1
      StrCpy $ProblemPath "$INSTDIR\${Relative} (linked directory)"
    ${EndIf}
  ${EndIf}
!macroend

!macro CheckWritable Relative
  ${If} ${FileExists} "$INSTDIR\${Relative}"
    System::Call 'kernel32::GetFileAttributesW(w "$INSTDIR\${Relative}") i.r0'
    IntOp $0 $0 & 0x400
    ${If} $0 != 0
      StrCpy $FileProblem 1
      StrCpy $ProblemPath "$INSTDIR\${Relative} (linked file)"
    ${Else}
    ; Check write/delete access without changing the file; loaded images and denied deletion must fail before removal.
    System::Call 'kernel32::CreateFileW(w "$INSTDIR\${Relative}", i 0x40010000, i 7, p 0, i 3, i 0x80, p 0) p.r0'
    ${If} $0 == -1
      StrCpy $FileProblem 1
      StrCpy $ProblemPath "$INSTDIR\${Relative}"
    ${Else}
      System::Call 'kernel32::CloseHandle(p r0)'
    ${EndIf}
    ${EndIf}
  ${EndIf}
!macroend

!macro Preflight
  StrCpy $FileProblem 0
  System::Call 'kernel32::GetFileAttributesW(w "$INSTDIR") i.r0'
  ${If} $0 != -1
    IntOp $0 $0 & 0x400
    ${If} $0 != 0
      StrCpy $FileProblem 1
      StrCpy $ProblemPath "$INSTDIR (linked folder)"
    ${EndIf}
  ${EndIf}
  !insertmacro KnownDirectories CheckDirectory
  ${If} $FileProblem == 0
    !insertmacro KnownFiles CheckWritable
    !insertmacro CheckWritable "Uninstall.exe"
    !insertmacro CheckWritable ".wtext-install"
    !insertmacro CheckWritable ".wtext-retained"
  ${EndIf}
  ${If} $FileProblem != 0
    MessageBox MB_ICONSTOP "Cannot change an installed file. Close wText and its preview windows, then retry.$\r$\n$ProblemPath$\r$\nNo installed files were removed." /SD IDOK
    SetErrorLevel 3
    Abort
  ${EndIf}
!macroend

!macro DeleteOwnedFile Relative
  ${If} ${FileExists} "$INSTDIR\${Relative}"
    Delete "$INSTDIR\${Relative}"
    ${If} ${FileExists} "$INSTDIR\${Relative}"
      StrCpy $FileProblem 1
      StrCpy $ProblemPath "$INSTDIR\${Relative}"
    ${EndIf}
  ${EndIf}
!macroend

!macro RemoveEmptyDirectory Relative
  RMDir "$INSTDIR\${Relative}"
!macroend

Section "wText" MainSection
  Call ValidateDirectory
  !insertmacro Preflight
  ; Establish ownership before extraction so a failed copy can be repaired in place.
  CreateDirectory "$INSTDIR"
  ClearErrors
  FileOpen $0 "$INSTDIR\.wtext-install" w
  FileWrite $0 "${APP_ID}"
  FileClose $0
  ${If} ${Errors}
    MessageBox MB_ICONSTOP "Could not write the installation marker. Check folder access and retry." /SD IDOK
    SetErrorLevel 3
    Abort
  ${EndIf}
  StrCpy $FileProblem 0
  !insertmacro ObsoleteFiles DeleteOwnedFile
  ${If} $FileProblem != 0
    MessageBox MB_ICONSTOP "An older file could not be removed. Retry after closing wText.$\r$\n$ProblemPath" /SD IDOK
    SetErrorLevel 3
    Abort
  ${EndIf}
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
  Delete "$INSTDIR\.wtext-retained"
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
  Call un.BeginMaintenance
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
  !insertmacro Preflight
  ClearErrors
  StrCpy $FileProblem 0
  !insertmacro KnownFiles DeleteOwnedFile
  ${If} $FileProblem != 0
    ; Retain the uninstaller, marker and registration until ALL owned files are gone.
    MessageBox MB_ICONSTOP "Some installed files could not be removed. Close wText and retry uninstalling.$\r$\n$ProblemPath" /SD IDOK
    SetErrorLevel 3
    Abort
  ${EndIf}
  !insertmacro KnownDirectories RemoveEmptyDirectory
  Delete "$INSTDIR\Uninstall.exe"
  ${If} ${FileExists} "$INSTDIR\Uninstall.exe"
    MessageBox MB_ICONSTOP "The uninstaller is still in use. Retry after closing the other installer window." /SD IDOK
    SetErrorLevel 3
    Abort
  ${EndIf}
  Delete "$INSTDIR\.wtext-install"
  Delete "$INSTDIR\.wtext-retained"
  RMDir "$INSTDIR"
  ${If} ${FileExists} "$INSTDIR\*"
    ; Only user-added files remain. Keep a small ownership receipt so reinstall is safe.
    FileOpen $0 "$INSTDIR\.wtext-retained" w
    FileWrite $0 "${APP_ID}"
    FileClose $0
    DetailPrint "User-added files were preserved in $INSTDIR."
  ${EndIf}
  Delete "$SMPROGRAMS\${APP_ID}\wText.lnk"
  Delete "$DESKTOP\${APP_ID}.lnk"
  RMDir "$SMPROGRAMS\${APP_ID}"
  DeleteRegKey HKCU "${UNINSTALL_KEY}"
  DeleteRegKey HKCU "Software\${APP_ID}"
SectionEnd
