; ══════════════════════════════════════════════════════════════════════════════
; Hearthlens setup wizard for Windows — Custom UI Edition (NSIS 3, MUI2)
; ══════════════════════════════════════════════════════════════════════════════
; Built by installer/windows/build.sh or PowerShell build script, which passes:
;   /DVERSION=x.y.z  /DSOURCE_DIR=<published app>  /DOUT_FILE=<setup exe>  /DUNINSTALL_LIST=<generated nsh>
; Per-user install (no administrator prompt) into %LOCALAPPDATA%\Programs\Hearthlens.

Unicode true
ManifestDPIAware true

!include "MUI2.nsh"
!include "FileFunc.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "WinVer.nsh"
!include "WinMessages.nsh"

!ifndef VERSION
  !define VERSION "0.3.0"
!endif
!ifndef SOURCE_DIR
  !error "Pass /DSOURCE_DIR=<folder with the published app>"
!endif
!ifndef OUT_FILE
  !define OUT_FILE "Hearthlens-Setup-${VERSION}-win-x64.exe"
!endif
!ifndef UNINSTALL_LIST
  !error "Pass /DUNINSTALL_LIST=<generated list of installed files>"
!endif

; ── App identity ─────────────────────────────────────────────────────────────
!define APP_NAME "Hearthlens"
!define APP_EXE "Hearthlens.exe"
!define UNINSTALLER "Uninstall Hearthlens.exe"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\Hearthlens"
!define RUN_KEY "Software\Microsoft\Windows\CurrentVersion\Run"
!define ASSETS "${__FILEDIR__}\..\assets"

; ── Installer properties ─────────────────────────────────────────────────────
Name "${APP_NAME}"
OutFile "${OUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\Hearthlens"
InstallDirRegKey HKCU "${UNINSTALL_KEY}" "InstallLocation"
RequestExecutionLevel user
SetCompressor /SOLID lzma
BrandingText "Hearthlens ${VERSION} • Hearthstone Companion"
ShowInstDetails show
ShowUninstDetails show

VIProductVersion "${VERSION}.0"
VIAddVersionKey /LANG=0 "ProductName" "Hearthlens"
VIAddVersionKey /LANG=0 "FileDescription" "Hearthlens Setup"
VIAddVersionKey /LANG=0 "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=0 "ProductVersion" "${VERSION}"
VIAddVersionKey /LANG=0 "LegalCopyright" "Hearthlens"

; ── UI Assets & Bitmaps ──────────────────────────────────────────────────────
!define MUI_ICON "${ASSETS}\hearthlens.ico"
!define MUI_UNICON "${ASSETS}\hearthlens.ico"

; Custom branded bitmaps with the actual Hearthlens emblem
!define MUI_WELCOMEFINISHPAGE_BITMAP "${ASSETS}\wizard-side.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "${ASSETS}\wizard-side.bmp"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_RIGHT
!define MUI_HEADERIMAGE_BITMAP "${ASSETS}\wizard-header.bmp"
!define MUI_HEADERIMAGE_UNBITMAP "${ASSETS}\wizard-header.bmp"

!define MUI_ABORTWARNING
!define MUI_UNABORTWARNING
!define MUI_COMPONENTSPAGE_SMALLDESC

; Enable 3-line titles on Welcome and Finish pages for elegant display
!define MUI_WELCOMEPAGE_TITLE_3LINES
!define MUI_FINISHPAGE_TITLE_3LINES

; Multi-language selection support
!define MUI_LANGDLL_ALLLANGUAGES
!define MUI_LANGDLL_REGISTRY_ROOT HKCU
!define MUI_LANGDLL_REGISTRY_KEY "Software\Hearthlens"
!define MUI_LANGDLL_REGISTRY_VALUENAME "InstallerLanguage"

; Hook for custom UI font enhancement
!define MUI_CUSTOMFUNCTION_GUIINIT OnSetupGUIInit

; ── Variables ────────────────────────────────────────────────────────────────
Var FontModernBold
Var FontModernRegular

; ── Pages: Installer ─────────────────────────────────────────────────────────
!define MUI_WELCOMEPAGE_TITLE "$(WelcomeTitle)"
!define MUI_WELCOMEPAGE_TEXT "$(WelcomeText)"
!insertmacro MUI_PAGE_WELCOME

!define MUI_PAGE_HEADER_TEXT "$(NoticeHeader)"
!define MUI_PAGE_HEADER_SUBTEXT "$(NoticeSubheader)"
!define MUI_LICENSEPAGE_BUTTON "$(^NextBtn)"
!define MUI_LICENSEPAGE_TEXT_TOP "$(NoticeTop)"
!define MUI_LICENSEPAGE_TEXT_BOTTOM "$(NoticeBottom)"
!insertmacro MUI_PAGE_LICENSE "${__FILEDIR__}\notice.txt"

!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES

!define MUI_FINISHPAGE_TITLE "$(FinishTitle)"
!define MUI_FINISHPAGE_TEXT "$(FinishText)"
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "$(RunNow)"
!define MUI_FINISHPAGE_LINK "Visit Hearthlens on GitHub"
!define MUI_FINISHPAGE_LINK_LOCATION "https://github.com/Dev-NotAqua/HeartLens"
!define MUI_FINISHPAGE_NOREBOOTSUPPORT
!insertmacro MUI_PAGE_FINISH

; ── Pages: Uninstaller ───────────────────────────────────────────────────────
!define MUI_UNWELCOMEPAGE_TITLE "$(UnWelcomeTitle)"
!define MUI_UNWELCOMEPAGE_TEXT "$(UnWelcomeText)"
!insertmacro MUI_UNPAGE_WELCOME
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_COMPONENTS
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

; ── Languages ────────────────────────────────────────────────────────────────
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "Italian"
!insertmacro MUI_RESERVEFILE_LANGDLL

; ── Localized strings ────────────────────────────────────────────────────────
LangString WelcomeTitle ${LANG_ENGLISH} "Welcome to Hearthlens ${VERSION}"
LangString WelcomeTitle ${LANG_ITALIAN} "Benvenuto in Hearthlens ${VERSION}"
LangString WelcomeText ${LANG_ENGLISH} "Hearthlens is a companion app for Hearthstone: an in-game overlay, deck tracker, Battlegrounds advisor (heroes, comps, shop, trinkets, Discover picks, combat odds) and match history.$\r$\n$\r$\nIt only reads Hearthstone's own log files and a few read-only values. It never modifies the game, never sends input to it, and never displays hidden information.$\r$\n$\r$\nClose Hearthlens before continuing.$\r$\n$\r$\nClick Next to proceed with setup."
LangString WelcomeText ${LANG_ITALIAN} "Hearthlens è un compagno per Hearthstone: un overlay in gioco, deck tracker, consigli per Battaglia (eroi, composizioni, locanda, monili, scelte Rinvenimento, probabilità di vittoria) e cronologia delle partite.$\r$\n$\r$\nLegge solo i log generati da Hearthstone e pochi valori in sola lettura. Non modifica mai il gioco e non mostra mai informazioni nascoste.$\r$\n$\r$\nChiudi Hearthlens prima di continuare.$\r$\n$\r$\nFai clic su Avanti per procedere con l'installazione."

LangString NoticeHeader ${LANG_ENGLISH} "Before You Install"
LangString NoticeHeader ${LANG_ITALIAN} "Prima di installare"
LangString NoticeSubheader ${LANG_ENGLISH} "How Hearthlens works, shortcuts and data credits."
LangString NoticeSubheader ${LANG_ITALIAN} "Come funziona Hearthlens, scorciatoie e crediti."
LangString NoticeTop ${LANG_ENGLISH} "Please review the notes and shortcuts below for Hearthlens."
LangString NoticeTop ${LANG_ITALIAN} "Leggi le note e le scorciatoie di seguito per Hearthlens."
LangString NoticeBottom ${LANG_ENGLISH} "Click Next to continue to component selection."
LangString NoticeBottom ${LANG_ITALIAN} "Fai clic su Avanti per selezionare i componenti."

LangString FinishTitle ${LANG_ENGLISH} "Hearthlens Setup Complete"
LangString FinishTitle ${LANG_ITALIAN} "Installazione di Hearthlens completata"
LangString FinishText ${LANG_ENGLISH} "Hearthlens ${VERSION} has been successfully installed on your computer.$\r$\n$\r$\nNote: If this is your first time using Hearthlens, restart Hearthstone once so that the game's log configuration can take effect.$\r$\n$\r$\nClick Finish to exit setup."
LangString FinishText ${LANG_ITALIAN} "Hearthlens ${VERSION} è stato installato con successo sul tuo computer.$\r$\n$\r$\nNota: se è la prima volta che usi Hearthlens, riavvia Hearthstone una volta per rendere attiva la configurazione dei log.$\r$\n$\r$\nFai clic su Fine per uscire dall'installazione."

LangString RunNow ${LANG_ENGLISH} "Launch Hearthlens now"
LangString RunNow ${LANG_ITALIAN} "Avvia Hearthlens adesso"

LangString SecAppName ${LANG_ENGLISH} "Hearthlens Application (Required)"
LangString SecAppName ${LANG_ITALIAN} "Applicazione Hearthlens (obbligatorio)"
LangString SecAppDesc ${LANG_ENGLISH} "The main Hearthlens app, Battlegrounds advisor, in-game overlay and uninstaller."
LangString SecAppDesc ${LANG_ITALIAN} "L'applicazione principale, consigliere per Battaglia, overlay in gioco e disinstallatore."

LangString SecDesktopName ${LANG_ENGLISH} "Desktop Shortcut"
LangString SecDesktopName ${LANG_ITALIAN} "Collegamento sul Desktop"
LangString SecDesktopDesc ${LANG_ENGLISH} "Creates a quick-launch shortcut on your desktop."
LangString SecDesktopDesc ${LANG_ITALIAN} "Crea un collegamento per l'avvio rapido sul desktop."

LangString SecMenuName ${LANG_ENGLISH} "Start Menu Shortcut"
LangString SecMenuName ${LANG_ITALIAN} "Collegamento nel Menu Start"
LangString SecMenuDesc ${LANG_ENGLISH} "Adds Hearthlens to your Windows Start menu programs list."
LangString SecMenuDesc ${LANG_ITALIAN} "Aggiunge Hearthlens all'elenco dei programmi nel menu Start."

LangString SecAutoStartName ${LANG_ENGLISH} "Start with Windows"
LangString SecAutoStartName ${LANG_ITALIAN} "Avvia all'avvio di Windows"
LangString SecAutoStartDesc ${LANG_ENGLISH} "Automatically launch Hearthlens when you sign in to Windows."
LangString SecAutoStartDesc ${LANG_ITALIAN} "Avvia automaticamente Hearthlens all'accesso a Windows."

LangString UnWelcomeTitle ${LANG_ENGLISH} "Uninstall Hearthlens"
LangString UnWelcomeTitle ${LANG_ITALIAN} "Disinstalla Hearthlens"
LangString UnWelcomeText ${LANG_ENGLISH} "This wizard will guide you through the uninstallation of Hearthlens ${VERSION}.$\r$\n$\r$\nClick Next to continue."
LangString UnWelcomeText ${LANG_ITALIAN} "Questa procedura guidata ti aiuterà a rimuovere Hearthlens ${VERSION}.$\r$\n$\r$\nFai clic su Avanti per continuare."

LangString UnSecAppName ${LANG_ENGLISH} "Hearthlens Core Files"
LangString UnSecAppName ${LANG_ITALIAN} "File principali di Hearthlens"
LangString UnSecAppDesc ${LANG_ENGLISH} "Removes the app binaries, shortcuts and autostart registration."
LangString UnSecAppDesc ${LANG_ITALIAN} "Rimuove i binari dell'applicazione, i collegamenti e l'avvio automatico."

LangString UnSecDataName ${LANG_ENGLISH} "Delete match history and settings"
LangString UnSecDataName ${LANG_ITALIAN} "Elimina cronologia partite e impostazioni"
LangString UnSecDataDesc ${LANG_ENGLISH} "Deletes your stored matches, deck configurations and cached card art in %APPDATA%\Hearthlens. Untick to preserve your data."
LangString UnSecDataDesc ${LANG_ITALIAN} "Elimina le partite salvate, le configurazioni e le immagini in cache in %APPDATA%\Hearthlens. Deseleziona per conservare i dati."

LangString ClosingApp ${LANG_ENGLISH} "Closing running Hearthlens instances…"
LangString ClosingApp ${LANG_ITALIAN} "Chiusura delle istanze attive di Hearthlens…"
LangString Needs64Bit ${LANG_ENGLISH} "Hearthlens requires 64-bit Windows 10 or Windows 11.$\r$\n$\r$\nThis machine is running 32-bit Windows, so Hearthlens cannot run here."
LangString Needs64Bit ${LANG_ITALIAN} "Hearthlens richiede Windows 10 o 11 a 64 bit.$\r$\n$\r$\nQuesto computer usa Windows a 32 bit e non può eseguire Hearthlens."
LangString NeedsWin10 ${LANG_ENGLISH} "Hearthlens requires Windows 10 (build 10240) or Windows 11."
LangString NeedsWin10 ${LANG_ITALIAN} "Hearthlens richiede Windows 10 (build 10240) o Windows 11."
LangString NeedsWin11Arm ${LANG_ENGLISH} "On ARM processor devices, Hearthlens requires Windows 11."
LangString NeedsWin11Arm ${LANG_ITALIAN} "Sui dispositivi con processore ARM, Hearthlens richiede Windows 11."

; ── Helper Macros ────────────────────────────────────────────────────────────
!macro CloseApp
  DetailPrint "$(ClosingApp)"
  nsExec::Exec '"$SYSDIR\taskkill.exe" /F /IM "${APP_EXE}"'
  Pop $0
  Sleep 800
!macroend

; ── GUI Initialization & Typography ──────────────────────────────────────────
Function OnSetupGUIInit
  ; Apply modern font hierarchy (Segoe UI)
  CreateFont $FontModernBold "Segoe UI" 12 700
  CreateFont $FontModernRegular "Segoe UI" 9 400

  ; Welcome / Finish page title ID is 1201
  GetDlgItem $0 $HWNDPARENT 1201
  ${If} $0 != 0
    SendMessage $0 ${WM_SETFONT} $FontModernBold 1
  ${EndIf}
FunctionEnd

Function .onInit
  !insertmacro MUI_LANGDLL_DISPLAY

  ; System requirement checks
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP|MB_OK "$(Needs64Bit)" /SD IDOK
    Abort
  ${EndIf}

  ${IfNot} ${AtLeastBuild} 10240
    MessageBox MB_ICONSTOP|MB_OK "$(NeedsWin10)" /SD IDOK
    Abort
  ${EndIf}

  ${If} ${IsNativeARM64}
  ${AndIfNot} ${AtLeastBuild} 22000
    MessageBox MB_ICONSTOP|MB_OK "$(NeedsWin11Arm)" /SD IDOK
    Abort
  ${EndIf}
FunctionEnd

Function un.onInit
  !insertmacro MUI_UNGETLANGUAGE
FunctionEnd

; ══════════════════════════════════════════════════════════════════════════════
; Installation Sections
; ══════════════════════════════════════════════════════════════════════════════

Section "$(SecAppName)" SecApp
  SectionIn RO
  !insertmacro CloseApp

  SetOutPath "$INSTDIR"
  File /r "${SOURCE_DIR}\*.*"
  WriteUninstaller "$INSTDIR\${UNINSTALLER}"

  ; Windows "Installed Apps" registration
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0

  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "Hearthlens"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "Hearthlens"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\${APP_EXE},0"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\${UNINSTALLER}"'
  WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\${UNINSTALLER}" /S'
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "EstimatedSize" "$0"
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
SectionEnd

Section "$(SecDesktopName)" SecDesktop
  CreateShortcut "$DESKTOP\Hearthlens.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
SectionEnd

Section "$(SecMenuName)" SecMenu
  CreateShortcut "$SMPROGRAMS\Hearthlens.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
SectionEnd

Section /o "$(SecAutoStartName)" SecAutoStart
  WriteRegStr HKCU "${RUN_KEY}" "Hearthlens" '"$INSTDIR\${APP_EXE}"'
SectionEnd

; Component descriptions
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecApp} "$(SecAppDesc)"
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} "$(SecDesktopDesc)"
  !insertmacro MUI_DESCRIPTION_TEXT ${SecMenu} "$(SecMenuDesc)"
  !insertmacro MUI_DESCRIPTION_TEXT ${SecAutoStart} "$(SecAutoStartDesc)"
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; ══════════════════════════════════════════════════════════════════════════════
; Uninstallation Sections
; ══════════════════════════════════════════════════════════════════════════════

Section "un.$(UnSecAppName)" UnSecApp
  SectionIn RO
  !insertmacro CloseApp

  Delete "$DESKTOP\Hearthlens.lnk"
  Delete "$SMPROGRAMS\Hearthlens.lnk"
  DeleteRegValue HKCU "${RUN_KEY}" "Hearthlens"

  ; Delete exact installed files list
  !include "${UNINSTALL_LIST}"
  Delete "$INSTDIR\${UNINSTALLER}"
  RMDir "$INSTDIR"

  DeleteRegKey HKCU "${UNINSTALL_KEY}"
  DeleteRegKey HKCU "Software\Hearthlens"
SectionEnd

Section /o "un.$(UnSecDataName)" UnSecData
  RMDir /r "$APPDATA\Hearthlens"
SectionEnd

!insertmacro MUI_UNFUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${UnSecApp} "$(UnSecAppDesc)"
  !insertmacro MUI_DESCRIPTION_TEXT ${UnSecData} "$(UnSecDataDesc)"
!insertmacro MUI_UNFUNCTION_DESCRIPTION_END
