Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "WinVer.nsh"

!ifndef APP_VERSION
  !define APP_VERSION "0.1.15"
!endif
!ifndef APP_SOURCE
  !error "APP_SOURCE must point to the Windows 8/8.1 package directory."
!endif
!ifndef APP_VERSION_NUMERIC
  !define APP_VERSION_NUMERIC "${APP_VERSION}.0"
!endif

!ifndef OUTPUT_FILE
  !define OUTPUT_FILE "ESD-Installer-Windows8-Setup-${APP_VERSION}.exe"
!endif
!ifndef APP_ICON
  !error "APP_ICON must point to ESDInstaller.Windows8.ico."
!endif
!ifndef APP_SIZE_KB
  !define APP_SIZE_KB 30000
!endif

!define APP_NAME "ESD Installer for Windows 8 and 8.1"
!define APP_PUBLISHER "A097MPRUS"
!define APP_REG_KEY "Software\ESDInstallerWindows8"
!define APP_UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\ESDInstallerWindows8"

Name "${APP_NAME} ${APP_VERSION}"
Caption "${APP_NAME} Setup"
OutFile "${OUTPUT_FILE}"
InstallDir "$PROGRAMFILES\ESD Installer Windows 8"
InstallDirRegKey HKLM "${APP_REG_KEY}" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
SetCompressorDictSize 32
CRCCheck on
ShowInstDetails show
ShowUninstDetails show
BrandingText "ESD Installer"
Icon "${APP_ICON}"
UninstallIcon "${APP_ICON}"
VIProductVersion "${APP_VERSION_NUMERIC}"
VIAddVersionKey /LANG=1033 "ProductName" "${APP_NAME}"
VIAddVersionKey /LANG=1033 "ProductVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "FileDescription" "ESD Installer Windows 8/8.1 Setup"
VIAddVersionKey /LANG=1033 "CompanyName" "${APP_PUBLISHER}"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Copyright (c) 2026 A097MPRUS"

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
; Long localized headings (notably Russian) need the supported three-line
; finish-page title area instead of being clipped at two lines.
!define MUI_FINISHPAGE_TITLE_3LINES
!define MUI_FINISHPAGE_RUN "$INSTDIR\ESDInstaller.Windows8.exe"
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
!insertmacro MUI_LANGUAGEEX "${__FILEDIR__}\..\..\installer\Languages" "Azerbaijani"
!insertmacro MUI_LANGUAGEEX "${__FILEDIR__}\..\..\installer\Languages" "Kyrgyz"
!insertmacro MUI_LANGUAGEEX "${__FILEDIR__}\..\..\installer\Languages" "UyghurCyrillic"
!insertmacro MUI_LANGUAGEEX "${__FILEDIR__}\..\..\installer\Languages" "Kazakh"
!insertmacro MUI_LANGUAGEEX "${__FILEDIR__}\..\..\installer\Languages" "Bashkir"
!insertmacro MUI_LANGUAGEEX "${__FILEDIR__}\..\..\installer\Languages" "CrimeanTatar"
!insertmacro MUI_LANGUAGEEX "${__FILEDIR__}\..\..\installer\Languages" "Abkhazian"
!insertmacro MUI_LANGUAGEEX "${__FILEDIR__}\..\..\installer\Languages" "Ossetian"

LangString UnsupportedWindows ${LANG_ENGLISH} "This ESD Installer edition runs only on Windows 8 and Windows 8.1."
LangString UnsupportedWindows ${LANG_FRENCH} "Cette édition d’ESD Installer fonctionne uniquement sous Windows 8 et Windows 8.1."
LangString UnsupportedWindows ${LANG_GERMAN} "Diese ESD Installer-Ausgabe läuft nur unter Windows 8 und Windows 8.1."
LangString UnsupportedWindows ${LANG_LUXEMBOURGISH} "Dës ESD Installer-Editioun leeft nëmmen op Windows 8 a Windows 8.1."
LangString UnsupportedWindows ${LANG_SERBIANLATIN} "Ovo izdanje programa ESD Installer radi samo na sistemima Windows 8 i Windows 8.1."
LangString UnsupportedWindows ${LANG_RUSSIAN} "Эта версия ESD Installer работает только в Windows 8 и Windows 8.1."
LangString UnsupportedWindows ${LANG_SIMPCHINESE} "此 ESD Installer 版本仅适用于 Windows 8 和 Windows 8.1。"
LangString UnsupportedWindows ${LANG_SPANISH} "Esta edición de ESD Installer solo funciona en Windows 8 y Windows 8.1."
LangString UnsupportedWindows ${LANG_POLISH} "Ta edycja ESD Installer działa tylko w systemach Windows 8 i Windows 8.1."
LangString UnsupportedWindows ${LANG_GREEK} "Αυτή η έκδοση του ESD Installer λειτουργεί μόνο σε Windows 8 και Windows 8.1."
LangString UnsupportedWindows ${LANG_DANISH} "Denne ESD Installer-udgave kører kun på Windows 8 og Windows 8.1."
LangString UnsupportedWindows ${LANG_NORWEGIAN} "Denne ESD Installer-utgaven kjører bare på Windows 8 og Windows 8.1."
LangString UnsupportedWindows ${LANG_FINNISH} "Tämä ESD Installer-versio toimii vain Windows 8:ssa ja Windows 8.1:ssä."
LangString UnsupportedWindows ${LANG_SWEDISH} "Den här ESD Installer-utgåvan körs endast på Windows 8 och Windows 8.1."
LangString UnsupportedWindows ${LANG_MONGOLIAN} "Энэ ESD Installer хувилбар зөвхөн Windows 8 болон Windows 8.1 дээр ажиллана."
LangString UnsupportedWindows ${LANG_ARMENIAN} "ESD Installer-ի այս տարբերակն աշխատում է միայն Windows 8-ում և Windows 8.1-ում։"
LangString UnsupportedWindows ${LANG_KAZAKH} "ESD Installer бағдарламасының бұл шығарылымы тек Windows 8 және Windows 8.1 жүйелерінде жұмыс істейді."
LangString UnsupportedWindows ${LANG_BASHKIR} "ESD Installer-ҙың был сығарылышы тик Windows 8 һәм Windows 8.1-ҙә эшләй."
LangString UnsupportedWindows ${LANG_TATAR} "ESD Installer-ның бу чыгарылышы Windows 8 һәм Windows 8.1-дә генә эшли."
LangString UnsupportedWindows ${LANG_CRIMEANTATAR} "ESD Installer-niñ bu sürümi tek Windows 8 ve Windows 8.1-de çalışa."
LangString UnsupportedWindows ${LANG_ABKHAZIAN} "ESD Installer ари аҭыжьымҭа Windows 8 ма Windows 8.1 мацара рҿы аус ауеит."
LangString UnsupportedWindows ${LANG_OSSETIAN} "Ацы ESD Installer-ы рауагъд æрмæст Windows 8 æмæ Windows 8.1-ы кусынц."
LangString UnsupportedWindows ${LANG_ARABIC} "يعمل إصدار ESD Installer هذا فقط على Windows 8 وWindows 8.1."
LangString UnsupportedWindows ${LANG_HEBREW} "מהדורת ESD Installer זו פועלת רק על Windows 8 ו-Windows 8.1."
LangString UnsupportedWindows ${LANG_FARSI} "این نسخه ESD Installer فقط روی Windows 8 و Windows 8.1 اجرا می‌شود."
LangString UnsupportedWindows ${LANG_AFRIKAANS} "Hierdie ESD Installer-uitgawe werk slegs op Windows 8 en Windows 8.1."
LangString UnsupportedWindows ${LANG_HUNGARIAN} "Ez a ESD Installer kiadás csak a Windows 8 és Windows 8.1 rendszeren fut."
LangString UnsupportedWindows ${LANG_PORTUGUESE} "Esta edição ESD Installer funciona apenas em Windows 8 e Windows 8.1."
LangString UnsupportedWindows ${LANG_CZECH} "Toto vydání ESD Installer běží pouze na Windows 8 a Windows 8.1."
LangString UnsupportedWindows ${LANG_TURKISH} "Bu ESD Installer sürümü yalnızca Windows 8 ve Windows 8.1'de çalışır."
LangString UnsupportedWindows ${LANG_THAI} "รุ่น ESD Installer นี้ทำงานบน Windows 8 และ Windows 8.1 เท่านั้น"
LangString UnsupportedWindows ${LANG_KOREAN} "이 ESD Installer 버전은 Windows 8 및 Windows 8.1에서만 실행됩니다."
LangString UnsupportedWindows ${LANG_JAPANESE} "この ESD Installer エディションは、Windows 8 および Windows 8.1 でのみ実行されます。"
LangString UnsupportedWindows ${LANG_GEORGIAN} "ეს ESD Installer გამოცემა მუშაობს მხოლოდ Windows 8-ზე და Windows 8.1-ზე."
LangString UnsupportedWindows ${LANG_TRADCHINESE} "此 ESD Installer 版本僅在 Windows 8 和 Windows 8.1 上運行。"
LangString UnsupportedWindows ${LANG_NORWEGIANNYNORSK} "Denne ESD Installer-utgaven kjører kun på Windows 8 og Windows 8.1."
LangString UnsupportedWindows ${LANG_ITALIAN} "Questa edizione di ESD Installer funziona solo su Windows 8 e Windows 8.1."
LangString UnsupportedWindows ${LANG_ROMANIAN} "Această ediție ESD Installer rulează numai pe Windows 8 și Windows 8.1."
LangString UnsupportedWindows ${LANG_ICELANDIC} "Þessi ESD Installer útgáfa keyrir aðeins á Windows 8 og Windows 8.1."
LangString UnsupportedWindows ${LANG_AZERBAIJANI} "Bu ESD Installer nəşri yalnız Windows 8 və Windows 8.1-də işləyir."
LangString UnsupportedWindows ${LANG_KYRGYZ} "Бул ESD Installer версиясы Windows 8 жана Windows 8.1де гана иштейт."
LangString UnsupportedWindows ${LANG_UYGHURCYRILLIC} "бу ESD Installer нәшири пәқәт Windows 8 вә Windows 8.1."
LangString DotNetRequired ${LANG_ENGLISH} "Microsoft .NET Framework 4.6.1 or later is required. Install it, restart Windows, and run Setup again."
LangString DotNetRequired ${LANG_FRENCH} "Microsoft .NET Framework 4.6.1 ou version ultérieure est requis. Installez-le, redémarrez Windows, puis relancez l’installation."
LangString DotNetRequired ${LANG_GERMAN} "Microsoft .NET Framework 4.6.1 oder neuer ist erforderlich. Installieren Sie es, starten Sie Windows neu und führen Sie Setup erneut aus."
LangString DotNetRequired ${LANG_LUXEMBOURGISH} "Microsoft .NET Framework 4.6.1 oder méi nei ass erfuerderlech. Installéiert et, start Windows nei a féiert de Setup nach eng Kéier aus."
LangString DotNetRequired ${LANG_SERBIANLATIN} "Potreban je Microsoft .NET Framework 4.6.1 ili noviji. Instalirajte ga, ponovo pokrenite Windows i zatim ponovo pokrenite instalaciju."
LangString DotNetRequired ${LANG_RUSSIAN} "Требуется Microsoft .NET Framework 4.6.1 или более поздней версии. Установите его, перезагрузите Windows и снова запустите установку."
LangString DotNetRequired ${LANG_SIMPCHINESE} "需要 Microsoft .NET Framework 4.6.1 或更高版本。请安装后重新启动 Windows，然后再次运行安装程序。"
LangString DotNetRequired ${LANG_SPANISH} "Se requiere Microsoft .NET Framework 4.6.1 o posterior. Instálelo, reinicie Windows y vuelva a ejecutar el instalador."
LangString DotNetRequired ${LANG_POLISH} "Wymagany jest Microsoft .NET Framework 4.6.1 lub nowszy. Zainstaluj go, uruchom ponownie Windows i ponownie uruchom instalator."
LangString DotNetRequired ${LANG_GREEK} "Απαιτείται Microsoft .NET Framework 4.6.1 ή νεότερο. Εγκαταστήστε το, επανεκκινήστε τα Windows και εκτελέστε ξανά την εγκατάσταση."
LangString DotNetRequired ${LANG_DANISH} "Microsoft .NET Framework 4.6.1 eller nyere er påkrævet. Installer det, genstart Windows, og kør installationen igen."
LangString DotNetRequired ${LANG_NORWEGIAN} "Microsoft .NET Framework 4.6.1 eller nyere kreves. Installer det, start Windows på nytt, og kjør installasjonsprogrammet igjen."
LangString DotNetRequired ${LANG_FINNISH} "Microsoft .NET Framework 4.6.1 tai uudempi vaaditaan. Asenna se, käynnistä Windows uudelleen ja suorita asennusohjelma uudelleen."
LangString DotNetRequired ${LANG_SWEDISH} "Microsoft .NET Framework 4.6.1 eller senare krävs. Installera det, starta om Windows och kör installationsprogrammet igen."
LangString DotNetRequired ${LANG_MONGOLIAN} "Microsoft .NET Framework 4.6.1 эсвэл түүнээс шинэ хувилбар шаардлагатай. Үүнийг суулгаж, Windows-ийг дахин эхлүүлээд суулгацыг дахин ажиллуулна уу."
LangString DotNetRequired ${LANG_ARMENIAN} "Պահանջվում է Microsoft .NET Framework 4.6.1 կամ ավելի նոր տարբերակ։ Տեղադրեք այն, վերագործարկեք Windows-ը և կրկին գործարկեք տեղադրիչը։"
LangString DotNetRequired ${LANG_KAZAKH} "Microsoft .NET Framework 4.6.1 немесе одан кейінгі нұсқасы қажет. Оны орнатып, Windows жүйесін қайта іске қосыңыз да, орнату бағдарламасын қайта іске қосыңыз."
LangString DotNetRequired ${LANG_BASHKIR} "Microsoft .NET Framework 4.6.1 йәки яңыраҡ версия кәрәк. Уны ҡуйығыҙ, Windows-ты яңынан эшләтеп ебәрегеҙ һәм урынлаштырыуҙы ҡабат эшләтегеҙ."
LangString DotNetRequired ${LANG_TATAR} "Microsoft .NET Framework 4.6.1 яки яңарак версия кирәк. Аны урнаштырыгыз, Windows-ны яңадан эшләтеп җибәрегез һәм урнаштыруны кабат эшләтегез."
LangString DotNetRequired ${LANG_CRIMEANTATAR} "Microsoft .NET Framework 4.6.1 ya da daa yañı sürüm kerek. Onı quruñız, Windows-nı kene başlatıñız ve qurucını kene çalıştırıñız."
LangString DotNetRequired ${LANG_ABKHAZIAN} "Microsoft .NET Framework 4.6.1 ма уи ишьҭанеиуа аверсиа аҭахуп. Ишьақәыргыланы, Windows еиҭаҿашәкны, ашьақәыргылара еиҭаҿашәкы."
LangString DotNetRequired ${LANG_OSSETIAN} "Microsoft .NET Framework 4.6.1 кæнæ фæстæдæр верси хъæуы. Йæ сæвæр, Windows ногæй райсæр æмæ сæвæрд ногæй райсæр."
LangString DotNetRequired ${LANG_ARABIC} "مطلوب Microsoft .NET Framework 4.6.1 أو الأحدث. قم بتثبيته، وأعد تشغيل Windows، ثم قم بتشغيل Setup مرة أخرى."
LangString DotNetRequired ${LANG_HEBREW} "נדרש Microsoft .NET Framework 4.6.1 ואילך. התקן אותו, הפעל מחדש את Windows והפעל שוב את Setup."
LangString DotNetRequired ${LANG_FARSI} "Microsoft .NET Framework 4.6.1 یا جدیدتر مورد نیاز است. آن را نصب کنید، Windows را مجددا راه اندازی کنید و دوباره Setup را اجرا کنید."
LangString DotNetRequired ${LANG_AFRIKAANS} "Microsoft .NET Framework 4.6.1 of later word vereis. Installeer dit, herbegin Windows en hardloop weer Setup."
LangString DotNetRequired ${LANG_HUNGARIAN} "Microsoft .NET Framework 4.6.1 vagy újabb verzió szükséges. Telepítse, indítsa újra a Windows-t, és futtassa újra a Setup-t."
LangString DotNetRequired ${LANG_PORTUGUESE} "Microsoft .NET Framework 4.6.1 ou posterior é necessário. Instale-o, reinicie o Windows e execute o Setup novamente."
LangString DotNetRequired ${LANG_CZECH} "Je vyžadován Microsoft .NET Framework 4.6.1 nebo novější. Nainstalujte jej, restartujte Windows a znovu spusťte Setup."
LangString DotNetRequired ${LANG_TURKISH} "Microsoft .NET Framework 4.6.1 veya üzeri gereklidir. Kurun, Windows'yi yeniden başlatın ve Setup'yi tekrar çalıştırın."
LangString DotNetRequired ${LANG_THAI} "ต้องใช้ Microsoft .NET Framework 4.6.1 หรือใหม่กว่า ติดตั้ง รีสตาร์ท Windows และรัน Setup อีกครั้ง"
LangString DotNetRequired ${LANG_KOREAN} "Microsoft .NET Framework 4.6.1 이상이 필요합니다. 이를 설치하고 Windows를 다시 시작한 후 Setup를 다시 실행하십시오."
LangString DotNetRequired ${LANG_JAPANESE} "Microsoft .NET Framework 4.6.1 以降が必要です。それをインストールし、Windows を再起動し、再び Setup を実行します。"
LangString DotNetRequired ${LANG_GEORGIAN} "საჭიროა Microsoft .NET Framework 4.6.1 ან უფრო ახალი. დააინსტალირეთ, გადატვირთეთ Windows და კვლავ გაუშვით Setup."
LangString DotNetRequired ${LANG_TRADCHINESE} "需要 Microsoft .NET Framework 4.6.1 或更高版本。安裝它，重新啟動Windows，然後再次運行Setup。"
LangString DotNetRequired ${LANG_NORWEGIANNYNORSK} "Microsoft .NET Framework 4.6.1 eller nyere kreves. Installer den, start Windows på nytt og kjør Setup igjen."
LangString DotNetRequired ${LANG_ITALIAN} "Microsoft .NET Framework 4.6.1 o versione successiva. Installalo, riavvia Windows ed esegui nuovamente Setup."
LangString DotNetRequired ${LANG_ROMANIAN} "Este necesar Microsoft .NET Framework 4.6.1 sau o versiune ulterioară. Instalați-l, reporniți Windows și rulați Setup din nou."
LangString DotNetRequired ${LANG_ICELANDIC} "Microsoft .NET Framework 4.6.1 eða nýrri er krafist. Settu það upp, endurræstu Windows og keyrðu Setup aftur."
LangString DotNetRequired ${LANG_AZERBAIJANI} "Microsoft .NET Framework 4.6.1 və ya sonrakı versiya tələb olunur. Onu quraşdırın, Windows-ni yenidən başladın və yenidən Setup-ni işə salın."
LangString DotNetRequired ${LANG_KYRGYZ} "Microsoft .NET Framework 4.6.1 же андан кийинкиси талап кылынат. Аны орнотуп, Windows-ди кайра иштетиңиз жана Setup-ди кайра иштетиңиз."
LangString DotNetRequired ${LANG_UYGHURCYRILLIC} "Microsoft .NET Framework 4.6.1 йаки униңдин кейин тәләп қилиниду. уни қачилаң , Windows ни қайта қозғитип , Setup ни қайта иҗра қилиң."

Function .onInit
  !insertmacro MUI_LANGDLL_DISPLAY
  ${IfNot} ${AtLeastWin8}
    MessageBox MB_OK|MB_ICONSTOP "$(UnsupportedWindows)"
    Abort
  ${EndIf}
  ${IfNot} ${AtMostWin8.1}
    MessageBox MB_OK|MB_ICONSTOP "$(UnsupportedWindows)"
    Abort
  ${EndIf}
  ${If} ${RunningX64}
    SetRegView 64
    StrCpy $INSTDIR "$PROGRAMFILES64\ESD Installer Windows 8"
  ${Else}
    SetRegView 32
  ${EndIf}
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  IntCmp $0 394254 dotnet_ok dotnet_missing dotnet_ok
  dotnet_missing:
    MessageBox MB_OK|MB_ICONSTOP "$(DotNetRequired)"
    Abort
  dotnet_ok:
  SetShellVarContext all
FunctionEnd

Section "ESD Installer for Windows 8 and 8.1" SEC_MAIN
  SectionIn RO
  SetShellVarContext all
  SetOverwrite on
  SetOutPath "$INSTDIR"
  File /r /x "*.pdb" /x "Redist" "${APP_SOURCE}\*.*"
  ; wimlib uses the Universal CRT. Windows 8/8.1 may not have KB2999226,
  ; so deploy Microsoft's down-level UCRT app-locally for the host architecture.
  ${If} ${RunningX64}
    SetOutPath "$INSTDIR"
    File "${APP_SOURCE}\Redist\UCRT\x64\*.dll"
    SetOutPath "$INSTDIR\Worker"
    File "${APP_SOURCE}\Redist\UCRT\x64\*.dll"
  ${Else}
    SetOutPath "$INSTDIR"
    File "${APP_SOURCE}\Redist\UCRT\x86\*.dll"
    SetOutPath "$INSTDIR\Worker"
    File "${APP_SOURCE}\Redist\UCRT\x86\*.dll"
  ${EndIf}
  SetOutPath "$INSTDIR"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\ESD Installer"
  CreateShortcut "$SMPROGRAMS\ESD Installer\ESD Installer for Windows 8 and 8.1.lnk" "$INSTDIR\ESDInstaller.Windows8.exe" "" "$INSTDIR\ESDInstaller.Windows8.exe" 0
  CreateShortcut "$DESKTOP\ESD Installer for Windows 8 and 8.1.lnk" "$INSTDIR\ESDInstaller.Windows8.exe" "" "$INSTDIR\ESDInstaller.Windows8.exe" 0
  WriteRegStr HKLM "${APP_REG_KEY}" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\ESDInstaller.Windows8.exe"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "UninstallString" "$\"$INSTDIR\Uninstall.exe$\""
  WriteRegDWORD HKLM "${APP_UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${APP_UNINSTALL_KEY}" "NoRepair" 1
  WriteRegDWORD HKLM "${APP_UNINSTALL_KEY}" "EstimatedSize" ${APP_SIZE_KB}
SectionEnd

Section "Uninstall"
  SetShellVarContext all
  Delete "$SMPROGRAMS\ESD Installer\ESD Installer for Windows 8 and 8.1.lnk"
  RMDir "$SMPROGRAMS\ESD Installer"
  Delete "$DESKTOP\ESD Installer for Windows 8 and 8.1.lnk"
  DeleteRegKey HKLM "${APP_UNINSTALL_KEY}"
  DeleteRegKey HKLM "${APP_REG_KEY}"
  RMDir /r "$INSTDIR"
SectionEnd

Function un.onInit
  !insertmacro MUI_UNGETLANGUAGE
  ${If} ${RunningX64}
    SetRegView 64
  ${Else}
    SetRegView 32
  ${EndIf}
  SetShellVarContext all
FunctionEnd
