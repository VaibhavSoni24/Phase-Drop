; ==============================================================================
; Phase Drop — Windows 11 Desktop Application NSIS Installer
; License: GPL-3.0-or-later
; ==============================================================================

Unicode True
RequestExecutionLevel user ; Per-user installation to prevent admin profile mismatch

!define PRODUCT_NAME "Phase Drop"
!ifndef PRODUCT_VERSION
!define PRODUCT_VERSION "1.0.0"
!endif
!define PRODUCT_PUBLISHER "Phase Drop Contributors"
!define PRODUCT_WEB_SITE "https://github.com/VaibhavSoni24/Phase-Drop"
!define PRODUCT_DIR_REGKEY "Software\PhaseDrop"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\PhaseDrop"
!define PRODUCT_UNINST_ROOT_KEY HKCU

; Modern UI 2
!include "MUI2.nsh"
!include "nsDialogs.nsh"
!include "LogicLib.nsh"

; MUI Settings
!define MUI_ABORTWARNING
!define MUI_ICON "..\..\assets\branding\icon.ico"
!define MUI_UNICON "..\..\assets\branding\icon.ico"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_BITMAP_NOSTRETCH

; Installer Pages
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\..\LICENSE"

; Custom Theme Selection Page
Page custom ThemePageCreate ThemePageLeave

!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES

; Finish Page options
!define MUI_FINISHPAGE_RUN "$INSTDIR\PhaseDrop.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Launch Phase Drop now"
!define MUI_FINISHPAGE_SHOWREADME ""
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Start Phase Drop automatically when logging into Windows"
!define MUI_FINISHPAGE_SHOWREADME_FUNCTION EnableStartupShortcut
!define MUI_FINISHPAGE_SHOWREADME_NOTCHECKED
!insertmacro MUI_PAGE_FINISH

; Uninstaller Pages
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

; Languages
!insertmacro MUI_LANGUAGE "English"

; Variables
Var Dialog
Var RadioSystem
Var RadioDark
Var RadioLight
Var SelectedTheme

Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "..\..\dist\PhaseDrop-Setup-v${PRODUCT_VERSION}-x64.exe"
InstallDir "$LOCALAPPDATA\Programs\PhaseDrop"
ShowInstDetails show
ShowUnInstDetails show

; ==============================================================================
; Custom Theme Selection Page Implementation
; ==============================================================================
Function ThemePageCreate
    nsDialogs::Create 1018
    Pop $Dialog
    ${If} $Dialog == error
        Abort
    ${EndIf}

    !insertmacro MUI_HEADER_TEXT "Choose Appearance Theme" "Select the initial theme for Phase Drop's radial menu and settings."

    ${NSD_CreateLabel} 0 0 100% 24u "Phase Drop features a modern Windows 11 Fluent design. Please select your preferred theme:"
    Pop $0

    ${NSD_CreateRadioButton} 10u 32u 90% 14u "System Default (Automatically matches your Windows 11 accent and mode)"
    Pop $RadioSystem
    ${NSD_Check} $RadioSystem ; Default selection

    ${NSD_CreateRadioButton} 10u 52u 90% 14u "Dark Theme (Sleek frosted glass with high contrast typography)"
    Pop $RadioDark

    ${NSD_CreateRadioButton} 10u 72u 90% 14u "Light Theme (Clean, bright surfaces with soft borders)"
    Pop $RadioLight

    nsDialogs::Show
FunctionEnd

Function ThemePageLeave
    ${NSD_GetState} $RadioDark $0
    ${If} $0 == ${BST_CHECKED}
        StrCpy $SelectedTheme "Dark"
    ${Else}
        ${NSD_GetState} $RadioLight $0
        ${If} $0 == ${BST_CHECKED}
            StrCpy $SelectedTheme "Light"
        ${Else}
            StrCpy $SelectedTheme "System"
        ${EndIf}
    ${EndIf}
FunctionEnd

Function EnableStartupShortcut
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "PhaseDrop" '"$INSTDIR\PhaseDrop.exe" --background'
FunctionEnd

; ==============================================================================
; Installation Section
; ==============================================================================
Section "MainSection" SEC01
    SetOutPath "$INSTDIR"
    SetOverwrite on

    ; Copy all published files
    File /r "..\..\artifacts\publish\*.*"

    ; Create Shortcuts
    CreateDirectory "$SMPROGRAMS\Phase Drop"
    CreateShortcut "$SMPROGRAMS\Phase Drop\Phase Drop.lnk" "$INSTDIR\PhaseDrop.exe" "" "$INSTDIR\Assets\icon.ico" 0
    CreateShortcut "$SMPROGRAMS\Phase Drop\Uninstall Phase Drop.lnk" "$INSTDIR\Uninstall.exe" "" "$INSTDIR\Uninstall.exe" 0
    CreateShortcut "$DESKTOP\Phase Drop.lnk" "$INSTDIR\PhaseDrop.exe" "" "$INSTDIR\Assets\icon.ico" 0

    ; Write Initial Theme into User Settings JSON ONLY if not already existing (preserve existing configuration during upgrade)
    IfFileExists "$LOCALAPPDATA\PhaseDrop\settings.json" SettingsExist
        CreateDirectory "$LOCALAPPDATA\PhaseDrop"
        FileOpen $0 "$LOCALAPPDATA\PhaseDrop\settings.json" w
        FileWrite $0 "{\r\n"
        FileWrite $0 '  "Theme": "$SelectedTheme",$\r$\n'
        FileWrite $0 '  "CustomOutputDirectory": null,$\r$\n'
        FileWrite $0 '  "MaxConcurrency": 4,$\r$\n'
        FileWrite $0 '  "LaunchAtStartup": false,$\r$\n'
        FileWrite $0 '  "ShowNotifications": true,$\r$\n'
        FileWrite $0 '  "KeepOriginalsSafe": true,$\r$\n'
        FileWrite $0 '  "DragHorizontalThreshold": 4,$\r$\n'
        FileWrite $0 '  "DragVerticalThreshold": 4,$\r$\n'
        FileWrite $0 '  "ShowWelcomeOnStartup": false$\r$\n'
        FileWrite $0 "}$\r$\n"
        FileClose $0
    SettingsExist:

    ; Create Uninstaller
    WriteUninstaller "$INSTDIR\Uninstall.exe"

    ; Register in Add/Remove Programs
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayName" "${PRODUCT_NAME}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayIcon" "$INSTDIR\Assets\icon.ico"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "URLInfoAbout" "${PRODUCT_WEB_SITE}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"
    WriteRegDWORD ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "NoModify" 1
    WriteRegDWORD ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "NoRepair" 1
SectionEnd

; ==============================================================================
; Uninstallation Section
; ==============================================================================
Section Uninstall
    ; Remove Shortcuts
    Delete "$DESKTOP\Phase Drop.lnk"
    Delete "$SMPROGRAMS\Phase Drop\Phase Drop.lnk"
    Delete "$SMPROGRAMS\Phase Drop\Uninstall Phase Drop.lnk"
    RMDir "$SMPROGRAMS\Phase Drop"

    ; Remove Run key if set
    DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "PhaseDrop"

    ; Remove Add/Remove Programs key
    DeleteRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}"

    ; Remove Application Files
    RMDir /r "$INSTDIR"

    ; Note: User created output files and settings in %LOCALAPPDATA%\PhaseDrop are preserved
SectionEnd
