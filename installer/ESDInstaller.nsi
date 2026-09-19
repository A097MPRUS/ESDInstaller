Unicode true

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "WinVer.nsh"

!ifndef APP_VERSION
  !define APP_VERSION "0.1.15"
!endif
!ifndef APP_SOURCE
  !error "APP_SOURCE must point to the self-contained ESDInstaller publish directory."
!endif
!ifndef APP_VERSION_NUMERIC
  !define APP_VERSION_NUMERIC "${APP_VERSION}.0"
!endif

!ifndef OUTPUT_FILE
  !define OUTPUT_FILE "ESD-Installer-Setup-${APP_VERSION}.exe"
!endif
!ifndef APP_ICON
  !error "APP_ICON must point to ESDInstaller.ico."
!endif
!ifndef APP_SIZE_KB
  !define APP_SIZE_KB 450000
!endif

!define APP_NAME "ESD Installer"
!define APP_PUBLISHER "ESD Installer Project"
!define APP_REG_KEY "Software\ESDInstaller"
!define APP_UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\ESDInstaller"

Name "${APP_NAME} ${APP_VERSION}"
Caption "${APP_NAME} Setup"
OutFile "${OUTPUT_FILE}"
InstallDir "$PROGRAMFILES64\ESD Installer"
InstallDirRegKey HKLM "${APP_REG_KEY}" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
SetCompressorDictSize 64
CRCCheck on
ShowInstDetails show
ShowUninstDetails show
AutoCloseWindow false
BrandingText "ESD Installer"
Icon "${APP_ICON}"
UninstallIcon "${APP_ICON}"
VIProductVersion "${APP_VERSION_NUMERIC}"
VIAddVersionKey /LANG=1033 "ProductName" "ESD Installer"
VIAddVersionKey /LANG=1033 "ProductVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "FileDescription" "ESD Installer Setup"
VIAddVersionKey /LANG=1033 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "CompanyName" "${APP_PUBLISHER}"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Copyright (c) 2026 ESD Installer Project"

!define MUI_ABORTWARNING
!define MUI_ICON "${APP_ICON}"
!define MUI_UNICON "${APP_ICON}"
!define MUI_LANGDLL_REGISTRY_ROOT HKLM
!define MUI_LANGDLL_REGISTRY_KEY "${APP_REG_KEY}"
!define MUI_LANGDLL_REGISTRY_VALUENAME "InstallerLanguage"

!define MUI_WELCOMEPAGE_TITLE_3LINES
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_TITLE_3LINES
!define MUI_FINISHPAGE_RUN "$INSTDIR\ESDInstaller.exe"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!define MUI_FINISHPAGE_TITLE_3LINES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "French"
!insertmacro MUI_LANGUAGE "German"
!insertmacro MUI_LANGUAGE "Luxembourgish"
!insertmacro MUI_LANGUAGE "SerbianLatin"
!insertmacro MUI_LANGUAGE "Russian"
!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "Spanish"
!insertmacro MUI_LANGUAGE "Polish"
!insertmacro MUI_LANGUAGE "Greek"
!insertmacro MUI_LANGUAGE "Danish"
!insertmacro MUI_LANGUAGE "Norwegian"
!insertmacro MUI_LANGUAGE "Finnish"
!insertmacro MUI_LANGUAGE "Swedish"
!insertmacro MUI_LANGUAGE "Mongolian"
!insertmacro MUI_LANGUAGE "Armenian"
!insertmacro MUI_LANGUAGE "Tatar"
!insertmacro MUI_LANGUAGE "Arabic"
!insertmacro MUI_LANGUAGE "Hebrew"
!insertmacro MUI_LANGUAGE "Farsi"
!insertmacro MUI_LANGUAGE "Afrikaans"
!insertmacro MUI_LANGUAGE "Hungarian"
!insertmacro MUI_LANGUAGE "Portuguese"
!insertmacro MUI_LANGUAGE "Czech"
!insertmacro MUI_LANGUAGE "Turkish"
!insertmacro MUI_LANGUAGE "Thai"
!insertmacro MUI_LANGUAGE "Korean"
!insertmacro MUI_LANGUAGE "Japanese"
!insertmacro MUI_LANGUAGE "Georgian"
!insertmacro MUI_LANGUAGE "TradChinese"
!insertmacro MUI_LANGUAGE "NorwegianNynorsk"
!insertmacro MUI_LANGUAGE "Italian"
!insertmacro MUI_LANGUAGE "Romanian"
!insertmacro MUI_LANGUAGE "Icelandic"
!insertmacro MUI_LANGUAGEEX "${__FILEDIR__}\Languages" "Azerbaijani"
!insertmacro MUI_LANGUAGEEX "${__FILEDIR__}\Languages" "Kyrgyz"
!insertmacro MUI_LANGUAGEEX "${__FILEDIR__}\Languages" "UyghurCyrillic"
!insertmacro MUI_LANGUAGEEX "${__FILEDIR__}\Languages" "Kazakh"
!insertmacro MUI_LANGUAGEEX "${__FILEDIR__}\Languages" "Bashkir"
!insertmacro MUI_LANGUAGEEX "${__FILEDIR__}\Languages" "CrimeanTatar"
!insertmacro MUI_LANGUAGEEX "${__FILEDIR__}\Languages" "Abkhazian"
!insertmacro MUI_LANGUAGEEX "${__FILEDIR__}\Languages" "Ossetian"

LangString UnsupportedWindows ${LANG_ENGLISH} "ESD Installer requires Windows 10 or Windows 11."
LangString UnsupportedWindows ${LANG_FRENCH} "ESD Installer nécessite Windows 10 ou Windows 11."
LangString UnsupportedWindows ${LANG_GERMAN} "ESD Installer erfordert Windows 10 oder Windows 11."
LangString UnsupportedWindows ${LANG_LUXEMBOURGISH} "ESD Installer erfuerdert Windows 10 oder Windows 11."
LangString UnsupportedWindows ${LANG_SERBIANLATIN} "ESD Installer zahteva Windows 10 ili Windows 11."
LangString UnsupportedWindows ${LANG_RUSSIAN} "Для ESD Installer требуется Windows 10 или Windows 11."
LangString UnsupportedWindows ${LANG_SIMPCHINESE} "ESD Installer 需要 Windows 10 或 Windows 11。"
LangString UnsupportedWindows ${LANG_SPANISH} "ESD Installer requiere Windows 10 o Windows 11."
LangString UnsupportedWindows ${LANG_POLISH} "ESD Installer wymaga systemu Windows 10 lub Windows 11."
LangString UnsupportedWindows ${LANG_GREEK} "Το ESD Installer απαιτεί Windows 10 ή Windows 11."
LangString UnsupportedWindows ${LANG_DANISH} "ESD Installer kræver Windows 10 eller Windows 11."
LangString UnsupportedWindows ${LANG_NORWEGIAN} "ESD Installer krever Windows 10 eller Windows 11."
LangString UnsupportedWindows ${LANG_FINNISH} "ESD Installer vaatii Windows 10:n tai Windows 11:n."
LangString UnsupportedWindows ${LANG_SWEDISH} "ESD Installer kräver Windows 10 eller Windows 11."
LangString UnsupportedWindows ${LANG_MONGOLIAN} "ESD Installer-д Windows 10 эсвэл Windows 11 шаардлагатай."
LangString UnsupportedWindows ${LANG_ARMENIAN} "ESD Installer-ի համար անհրաժեշտ է Windows 10 կամ Windows 11։"
LangString UnsupportedWindows ${LANG_KAZAKH} "ESD Installer бағдарламасына Windows 10 немесе Windows 11 қажет."
LangString UnsupportedWindows ${LANG_BASHKIR} "ESD Installer өсөн Windows 10 йәки Windows 11 кәрәк."
LangString UnsupportedWindows ${LANG_TATAR} "ESD Installer өчен Windows 10 яки Windows 11 кирәк."
LangString UnsupportedWindows ${LANG_CRIMEANTATAR} "ESD Installer içün Windows 10 ya da Windows 11 kerek."
LangString UnsupportedWindows ${LANG_ABKHAZIAN} "ESD Installer азы Windows 10 ма Windows 11 аҭахуп."
LangString UnsupportedWindows ${LANG_OSSETIAN} "ESD Installer-æн Windows 10 кæнæ Windows 11 хъæуы."
LangString UnsupportedWindows ${LANG_ARABIC} "يتطلب ESD Installer Windows 10 أو Windows 11."
LangString UnsupportedWindows ${LANG_HEBREW} "ESD Installer דורש Windows 10 או Windows 11."
LangString UnsupportedWindows ${LANG_FARSI} "ESD Installer به Windows 10 یا Windows 11 نیاز دارد."
LangString UnsupportedWindows ${LANG_AFRIKAANS} "ESD Installer vereis Windows 10 of Windows 11."
LangString UnsupportedWindows ${LANG_HUNGARIAN} "A ESD Installer-hez a Windows 10 vagy a Windows 11 szükséges."
LangString UnsupportedWindows ${LANG_PORTUGUESE} "ESD Installer requer Windows 10 ou Windows 11."
LangString UnsupportedWindows ${LANG_CZECH} "ESD Installer vyžaduje Windows 10 nebo Windows 11."
LangString UnsupportedWindows ${LANG_TURKISH} "ESD Installer, Windows 10 veya Windows 11 gerektirir."
LangString UnsupportedWindows ${LANG_THAI} "ESD Installer ต้องใช้ Windows 10 หรือ Windows 11"
LangString UnsupportedWindows ${LANG_KOREAN} "ESD Installer에는 Windows 10 또는 Windows 11이 필요합니다."
LangString UnsupportedWindows ${LANG_JAPANESE} "ESD Installer には、Windows 10 または Windows 11 が必要です。"
LangString UnsupportedWindows ${LANG_GEORGIAN} "ESD Installer მოითხოვს Windows 10 ან Windows 11."
LangString UnsupportedWindows ${LANG_TRADCHINESE} "ESD Installer 需要 Windows 10 或 Windows 11。"
LangString UnsupportedWindows ${LANG_NORWEGIANNYNORSK} "ESD Installer krever Windows 10 eller Windows 11."
LangString UnsupportedWindows ${LANG_ITALIAN} "ESD Installer richiede Windows 10 o Windows 11."
LangString UnsupportedWindows ${LANG_ROMANIAN} "ESD Installer necesită Windows 10 sau Windows 11."
LangString UnsupportedWindows ${LANG_ICELANDIC} "ESD Installer krefst Windows 10 eða Windows 11."
LangString UnsupportedWindows ${LANG_AZERBAIJANI} "ESD Installer Windows 10 və ya Windows 11 tələb edir."
LangString UnsupportedWindows ${LANG_KYRGYZ} "ESD Installer үчүн Windows 10 же Windows 11 талап кылынат."
LangString UnsupportedWindows ${LANG_UYGHURCYRILLIC} "ESD Installer Windows 10 йаки Windows 11 ни тәләп қилиду."
LangString UnsupportedArchitecture ${LANG_ENGLISH} "This ESD Installer build requires 64-bit Windows (x64)."
LangString UnsupportedArchitecture ${LANG_FRENCH} "Cette version d’ESD Installer nécessite Windows 64 bits (x64)."
LangString UnsupportedArchitecture ${LANG_GERMAN} "Diese ESD Installer-Version erfordert 64-Bit-Windows (x64)."
LangString UnsupportedArchitecture ${LANG_LUXEMBOURGISH} "Dës ESD Installer-Versioun erfuerdert 64-Bit-Windows (x64)."
LangString UnsupportedArchitecture ${LANG_SERBIANLATIN} "Ova verzija programa ESD Installer zahteva 64-bitni Windows (x64)."
LangString UnsupportedArchitecture ${LANG_RUSSIAN} "Для этой версии ESD Installer требуется 64-разрядная Windows (x64)."
LangString UnsupportedArchitecture ${LANG_SIMPCHINESE} "此 ESD Installer 版本需要 64 位 Windows (x64)。"
LangString UnsupportedArchitecture ${LANG_SPANISH} "Esta versión de ESD Installer requiere Windows de 64 bits (x64)."
LangString UnsupportedArchitecture ${LANG_POLISH} "Ta wersja ESD Installer wymaga 64-bitowego systemu Windows (x64)."
LangString UnsupportedArchitecture ${LANG_GREEK} "Αυτή η έκδοση του ESD Installer απαιτεί Windows 64 bit (x64)."
LangString UnsupportedArchitecture ${LANG_DANISH} "Denne version af ESD Installer kræver 64-bit Windows (x64)."
LangString UnsupportedArchitecture ${LANG_NORWEGIAN} "Denne ESD Installer-versjonen krever 64-biters Windows (x64)."
LangString UnsupportedArchitecture ${LANG_FINNISH} "Tämä ESD Installer-versio vaatii 64-bittisen Windowsin (x64)."
LangString UnsupportedArchitecture ${LANG_SWEDISH} "Den här ESD Installer-versionen kräver 64-bitars Windows (x64)."
LangString UnsupportedArchitecture ${LANG_MONGOLIAN} "Энэ ESD Installer хувилбарт 64 битийн Windows (x64) шаардлагатай."
LangString UnsupportedArchitecture ${LANG_ARMENIAN} "ESD Installer-ի այս տարբերակի համար անհրաժեշտ է 64-բիթանոց Windows (x64)։"
LangString UnsupportedArchitecture ${LANG_KAZAKH} "ESD Installer бағдарламасының бұл нұсқасына 64 биттік Windows (x64) қажет."
LangString UnsupportedArchitecture ${LANG_BASHKIR} "ESD Installer-ҙың был версияһы 64-битлы Windows (x64) талап итә."
LangString UnsupportedArchitecture ${LANG_TATAR} "ESD Installer-ның бу версиясе 64 битлы Windows (x64) таләп итә."
LangString UnsupportedArchitecture ${LANG_CRIMEANTATAR} "ESD Installer-niñ bu sürümi 64-bit Windows (x64) talap ete."
LangString UnsupportedArchitecture ${LANG_ABKHAZIAN} "ESD Installer ари аверсиа 64-биттә Windows (x64) аҭахуп."
LangString UnsupportedArchitecture ${LANG_OSSETIAN} "Ацы ESD Installer-ы версийæн 64-битон Windows (x64) хъæуы."
LangString UnsupportedArchitecture ${LANG_ARABIC} "يتطلب إصدار ESD Installer هذا Windows (x64) 64 بت."
LangString UnsupportedArchitecture ${LANG_HEBREW} "מבנה ESD Installer זה דורש 64 סיביות Windows (x64)."
LangString UnsupportedArchitecture ${LANG_FARSI} "این ساخت ESD Installer به 64 بیت Windows (x64) نیاز دارد."
LangString UnsupportedArchitecture ${LANG_AFRIKAANS} "Hierdie ESD Installer-bou vereis 64-bis Windows (x64)."
LangString UnsupportedArchitecture ${LANG_HUNGARIAN} "Ehhez a ESD Installer buildhez 64 bites Windows (x64) szükséges."
LangString UnsupportedArchitecture ${LANG_PORTUGUESE} "Esta compilação ESD Installer requer Windows (x64) de 64 bits."
LangString UnsupportedArchitecture ${LANG_CZECH} "Tato sestava ESD Installer vyžaduje 64bitovou verzi Windows (x64)."
LangString UnsupportedArchitecture ${LANG_TURKISH} "Bu ESD Installer yapısı 64 bit Windows (x64) gerektirir."
LangString UnsupportedArchitecture ${LANG_THAI} "บิลด์ ESD Installer นี้ต้องการ Windows 64 บิต (x64)"
LangString UnsupportedArchitecture ${LANG_KOREAN} "이 ESD Installer 빌드에는 64비트 Windows(x64)가 필요합니다."
LangString UnsupportedArchitecture ${LANG_JAPANESE} "この ESD Installer ビルドには 64 ビット Windows (x64) が必要です。"
LangString UnsupportedArchitecture ${LANG_GEORGIAN} "ამ ESD Installer კონსტრუქციას სჭირდება 64-ბიტიანი Windows (x64)."
LangString UnsupportedArchitecture ${LANG_TRADCHINESE} "此 ESD Installer 建置需要 64 位元 Windows (x64)。"
LangString UnsupportedArchitecture ${LANG_NORWEGIANNYNORSK} "Denne ESD Installer-bygningen krever 64-bits Windows (x64)."
LangString UnsupportedArchitecture ${LANG_ITALIAN} "Questa build ESD Installer richiede Windows a 64 bit (x64)."
LangString UnsupportedArchitecture ${LANG_ROMANIAN} "Această versiune ESD Installer necesită Windows pe 64 de biți (x64)."
LangString UnsupportedArchitecture ${LANG_ICELANDIC} "Þessi ESD Installer smíði krefst 64 bita Windows (x64)."
LangString UnsupportedArchitecture ${LANG_AZERBAIJANI} "Bu ESD Installer quruluşu 64-bit Windows (x64) tələb edir."
LangString UnsupportedArchitecture ${LANG_KYRGYZ} "Бул ESD Installer түзүлүшү 64-бит Windows (x64) талап кылынат."
LangString UnsupportedArchitecture ${LANG_UYGHURCYRILLIC} "бу ESD Installer йасаш 64 битлиқ Windows (x64) ни тәләп қилиду."

Function .onInit
  !insertmacro MUI_LANGDLL_DISPLAY
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_OK|MB_ICONSTOP "$(UnsupportedWindows)"
    Abort
  ${EndIf}
  ${IfNot} ${RunningX64}
    MessageBox MB_OK|MB_ICONSTOP "$(UnsupportedArchitecture)"
    Abort
  ${EndIf}
  SetRegView 64
  SetShellVarContext all
FunctionEnd

Section "ESD Installer" SEC_MAIN
  SectionIn RO
  SetRegView 64
  SetShellVarContext all
  SetOverwrite on
  SetOutPath "$INSTDIR"
  File /r /x "*.pdb" "${APP_SOURCE}\*.*"

  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\ESD Installer"
  CreateShortcut "$SMPROGRAMS\ESD Installer\ESD Installer.lnk" "$INSTDIR\ESDInstaller.exe" "" "$INSTDIR\ESDInstaller.exe" 0
  CreateShortcut "$SMPROGRAMS\ESD Installer\Uninstall ESD Installer.lnk" "$INSTDIR\Uninstall.exe"

  WriteRegStr HKLM "${APP_REG_KEY}" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\ESDInstaller.exe"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "UninstallString" "$\"$INSTDIR\Uninstall.exe$\""
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "QuietUninstallString" "$\"$INSTDIR\Uninstall.exe$\" /S"
  WriteRegDWORD HKLM "${APP_UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${APP_UNINSTALL_KEY}" "NoRepair" 1
  WriteRegDWORD HKLM "${APP_UNINSTALL_KEY}" "EstimatedSize" ${APP_SIZE_KB}
SectionEnd

Section "Uninstall"
  SetRegView 64
  SetShellVarContext all
  Delete "$SMPROGRAMS\ESD Installer\ESD Installer.lnk"
  Delete "$SMPROGRAMS\ESD Installer\Uninstall ESD Installer.lnk"
  RMDir "$SMPROGRAMS\ESD Installer"
  DeleteRegKey HKLM "${APP_UNINSTALL_KEY}"
  DeleteRegKey HKLM "${APP_REG_KEY}"
  RMDir /r "$INSTDIR"
SectionEnd

Function un.onInit
  !insertmacro MUI_UNGETLANGUAGE
  SetRegView 64
  SetShellVarContext all
FunctionEnd
