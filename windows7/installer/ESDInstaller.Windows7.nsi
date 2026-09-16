Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "WinVer.nsh"

!ifndef APP_VERSION
  !define APP_VERSION "0.1.14"
!endif
!ifndef APP_SOURCE
  !error "APP_SOURCE must point to the Windows 7 package directory."
!endif
!ifndef APP_VERSION_NUMERIC
  !define APP_VERSION_NUMERIC "${APP_VERSION}.0"
!endif

!ifndef OUTPUT_FILE
  !define OUTPUT_FILE "ESD-Installer-Windows7-Setup-${APP_VERSION}.exe"
!endif
!ifndef APP_ICON
  !error "APP_ICON must point to ESDInstaller.Windows7.ico."
!endif
!ifndef APP_SIZE_KB
  !define APP_SIZE_KB 30000
!endif

!define APP_NAME "ESD Installer for Windows 7"
!define APP_PUBLISHER "A097MPRUS"
!define APP_REG_KEY "Software\ESDInstallerWindows7"
!define APP_UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\ESDInstallerWindows7"

Name "${APP_NAME} ${APP_VERSION}"
Caption "${APP_NAME} Setup"
OutFile "${OUTPUT_FILE}"
InstallDir "$PROGRAMFILES\ESD Installer Windows 7"
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
VIAddVersionKey /LANG=1033 "FileDescription" "ESD Installer Windows 7 Setup"
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
!define MUI_FINISHPAGE_TITLE_3LINES
!define MUI_FINISHPAGE_RUN "$INSTDIR\ESDInstaller.Windows7.exe"
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

LangString UnsupportedWindows ${LANG_ENGLISH} "ESD Installer for Windows 7 requires Windows 7 SP1 or later."
LangString UnsupportedWindows ${LANG_FRENCH} "ESD Installer pour Windows 7 nécessite Windows 7 SP1 ou une version ultérieure."
LangString UnsupportedWindows ${LANG_GERMAN} "ESD Installer für Windows 7 erfordert Windows 7 SP1 oder neuer."
LangString UnsupportedWindows ${LANG_LUXEMBOURGISH} "ESD Installer fir Windows 7 erfuerdert Windows 7 SP1 oder méi nei."
LangString UnsupportedWindows ${LANG_SERBIANLATIN} "ESD Installer za Windows 7 zahteva Windows 7 SP1 ili noviji."
LangString UnsupportedWindows ${LANG_RUSSIAN} "ESD Installer для Windows 7 требует Windows 7 SP1 или более новую версию."
LangString UnsupportedWindows ${LANG_SIMPCHINESE} "ESD Installer Windows 7 版需要 Windows 7 SP1 或更高版本。"
LangString UnsupportedWindows ${LANG_SPANISH} "ESD Installer para Windows 7 requiere Windows 7 SP1 o posterior."
LangString UnsupportedWindows ${LANG_POLISH} "ESD Installer dla Windows 7 wymaga Windows 7 SP1 lub nowszego."
LangString UnsupportedWindows ${LANG_GREEK} "Το ESD Installer για Windows 7 απαιτεί Windows 7 SP1 ή νεότερη έκδοση."
LangString UnsupportedWindows ${LANG_DANISH} "ESD Installer til Windows 7 kræver Windows 7 SP1 eller nyere."
LangString UnsupportedWindows ${LANG_NORWEGIAN} "ESD Installer for Windows 7 krever Windows 7 SP1 eller nyere."
LangString UnsupportedWindows ${LANG_FINNISH} "ESD Installer for Windows 7 vaatii Windows 7 SP1:n tai uudemman."
LangString UnsupportedWindows ${LANG_SWEDISH} "ESD Installer för Windows 7 kräver Windows 7 SP1 eller senare."
LangString UnsupportedWindows ${LANG_MONGOLIAN} "Windows 7-д зориулсан ESD Installer нь Windows 7 SP1 эсвэл түүнээс шинэ хувилбар шаарддаг."
LangString UnsupportedWindows ${LANG_ARMENIAN} "Windows 7-ի համար ESD Installer-ը պահանջում է Windows 7 SP1 կամ ավելի նոր տարբերակ։"
LangString UnsupportedWindows ${LANG_KAZAKH} "Windows 7 жүйесіне арналған ESD Installer бағдарламасына Windows 7 SP1 немесе одан кейінгі нұсқа қажет."
LangString UnsupportedWindows ${LANG_BASHKIR} "Windows 7 өсөн ESD Installer Windows 7 SP1 йәки яңыраҡ версия талап итә."
LangString UnsupportedWindows ${LANG_TATAR} "Windows 7 өчен ESD Installer Windows 7 SP1 яки яңарак версия таләп итә."
LangString UnsupportedWindows ${LANG_CRIMEANTATAR} "Windows 7 içün ESD Installer Windows 7 SP1 ya da daa yañı sürüm talap ete."
LangString UnsupportedWindows ${LANG_ABKHAZIAN} "Windows 7 азы ESD Installer Windows 7 SP1 ма уи ишьҭанеиуа аверсиа аҭахуп."
LangString UnsupportedWindows ${LANG_OSSETIAN} "Windows 7-ы ESD Installer-æн Windows 7 SP1 кæнæ фæстæдæр верси хъæуы."
LangString UnsupportedWindows ${LANG_ARABIC} "يعمل إصدار ESD Installer هذا فقط على Windows 7 SP1 أو الإصدارات الأحدث."
LangString UnsupportedWindows ${LANG_HEBREW} "מהדורת ESD Installer זו פועלת רק על Windows 7 SP1 ואילך."
LangString UnsupportedWindows ${LANG_FARSI} "این نسخه ESD Installer فقط در Windows 7 SP1 یا جدیدتر اجرا می‌شود."
LangString UnsupportedWindows ${LANG_AFRIKAANS} "Hierdie ESD Installer-uitgawe werk slegs op Windows 7 SP1 of later."
LangString UnsupportedWindows ${LANG_HUNGARIAN} "Ez a ESD Installer kiadás csak a Windows 7 SP1 vagy újabb verziókon fut."
LangString UnsupportedWindows ${LANG_PORTUGUESE} "Esta edição ESD Installer é executada apenas em Windows 7 SP1 ou posterior."
LangString UnsupportedWindows ${LANG_CZECH} "Toto vydání ESD Installer běží pouze na Windows 7 SP1 nebo novější."
LangString UnsupportedWindows ${LANG_TURKISH} "Bu ESD Installer sürümü yalnızca Windows 7 SP1 veya sonraki sürümlerde çalışır."
LangString UnsupportedWindows ${LANG_THAI} "รุ่น ESD Installer นี้ทำงานบน Windows 7 SP1 หรือใหม่กว่าเท่านั้น"
LangString UnsupportedWindows ${LANG_KOREAN} "이 ESD Installer 버전은 Windows 7 SP1 이상에서만 실행됩니다."
LangString UnsupportedWindows ${LANG_JAPANESE} "この ESD Installer エディションは、Windows 7 SP1 以降でのみ実行されます。"
LangString UnsupportedWindows ${LANG_GEORGIAN} "ეს ESD Installer გამოცემა მუშაობს მხოლოდ Windows 7 SP1-ზე ან უფრო ახალზე."
LangString UnsupportedWindows ${LANG_TRADCHINESE} "此 ESD Installer 版本僅在 Windows 7 SP1 或更高版本上運行。"
LangString UnsupportedWindows ${LANG_NORWEGIANNYNORSK} "Denne ESD Installer-utgaven kjører kun på Windows 7 SP1 eller nyere."
LangString UnsupportedWindows ${LANG_ITALIAN} "Questa edizione di ESD Installer funziona solo su Windows 7 SP1 o versioni successive."
LangString UnsupportedWindows ${LANG_ROMANIAN} "Această ediție ESD Installer rulează numai pe Windows 7 SP1 sau o versiune ulterioară."
LangString UnsupportedWindows ${LANG_ICELANDIC} "Þessi ESD Installer útgáfa keyrir aðeins á Windows 7 SP1 eða nýrri."
LangString UnsupportedWindows ${LANG_AZERBAIJANI} "Bu ESD Installer nəşri yalnız Windows 7 SP1 və ya daha sonrakı versiyalarda işləyir."
LangString UnsupportedWindows ${LANG_KYRGYZ} "Бул ESD Installer версиясы Windows 7 SP1 же андан кийинки версияларында гана иштейт."
LangString UnsupportedWindows ${LANG_UYGHURCYRILLIC} "бу ESD Installer нәшири пәқәт Windows 7 SP1 йаки униңдин йуқири нәшридә иҗра болиду."
LangString DotNetRequired ${LANG_ENGLISH} "Microsoft .NET Framework 4.8 is required. Install it, restart Windows, and run Setup again."
LangString DotNetRequired ${LANG_FRENCH} "Microsoft .NET Framework 4.8 est requis. Installez-le, redémarrez Windows, puis relancez l’installation."
LangString DotNetRequired ${LANG_GERMAN} "Microsoft .NET Framework 4.8 ist erforderlich. Installieren Sie es, starten Sie Windows neu und führen Sie Setup erneut aus."
LangString DotNetRequired ${LANG_LUXEMBOURGISH} "Microsoft .NET Framework 4.8 ass erfuerderlech. Installéiert et, start Windows nei a féiert de Setup nach eng Kéier aus."
LangString DotNetRequired ${LANG_SERBIANLATIN} "Potreban je Microsoft .NET Framework 4.8. Instalirajte ga, ponovo pokrenite Windows i zatim ponovo pokrenite instalaciju."
LangString DotNetRequired ${LANG_RUSSIAN} "Требуется Microsoft .NET Framework 4.8. Установите его, перезагрузите Windows и снова запустите установку."
LangString DotNetRequired ${LANG_SIMPCHINESE} "需要 Microsoft .NET Framework 4.8。请安装后重新启动 Windows，然后再次运行安装程序。"
LangString DotNetRequired ${LANG_SPANISH} "Se requiere Microsoft .NET Framework 4.8. Instálelo, reinicie Windows y vuelva a ejecutar el instalador."
LangString DotNetRequired ${LANG_POLISH} "Wymagany jest Microsoft .NET Framework 4.8. Zainstaluj go, uruchom ponownie Windows i ponownie uruchom instalator."
LangString DotNetRequired ${LANG_GREEK} "Απαιτείται το Microsoft .NET Framework 4.8. Εγκαταστήστε το, επανεκκινήστε τα Windows και εκτελέστε ξανά την εγκατάσταση."
LangString DotNetRequired ${LANG_DANISH} "Microsoft .NET Framework 4.8 er påkrævet. Installer det, genstart Windows, og kør installationen igen."
LangString DotNetRequired ${LANG_NORWEGIAN} "Microsoft .NET Framework 4.8 kreves. Installer det, start Windows på nytt, og kjør installasjonsprogrammet igjen."
LangString DotNetRequired ${LANG_FINNISH} "Microsoft .NET Framework 4.8 vaaditaan. Asenna se, käynnistä Windows uudelleen ja suorita asennusohjelma uudelleen."
LangString DotNetRequired ${LANG_SWEDISH} "Microsoft .NET Framework 4.8 krävs. Installera det, starta om Windows och kör installationsprogrammet igen."
LangString DotNetRequired ${LANG_MONGOLIAN} "Microsoft .NET Framework 4.8 шаардлагатай. Үүнийг суулгаж, Windows-ийг дахин эхлүүлээд суулгацыг дахин ажиллуулна уу."
LangString DotNetRequired ${LANG_ARMENIAN} "Պահանջվում է Microsoft .NET Framework 4.8։ Տեղադրեք այն, վերագործարկեք Windows-ը և կրկին գործարկեք տեղադրիչը։"
LangString DotNetRequired ${LANG_KAZAKH} "Microsoft .NET Framework 4.8 қажет. Оны орнатып, Windows жүйесін қайта іске қосыңыз да, орнату бағдарламасын қайта іске қосыңыз."
LangString DotNetRequired ${LANG_BASHKIR} "Microsoft .NET Framework 4.8 кәрәк. Уны ҡуйығыҙ, Windows-ты яңынан эшләтеп ебәрегеҙ һәм урынлаштырыуҙы ҡабат эшләтегеҙ."
LangString DotNetRequired ${LANG_TATAR} "Microsoft .NET Framework 4.8 кирәк. Аны урнаштырыгыз, Windows-ны яңадан эшләтеп җибәрегез һәм урнаштыруны кабат эшләтегез."
LangString DotNetRequired ${LANG_CRIMEANTATAR} "Microsoft .NET Framework 4.8 kerek. Onı quruñız, Windows-nı kene başlatıñız ve qurucını kene çalıştırıñız."
LangString DotNetRequired ${LANG_ABKHAZIAN} "Microsoft .NET Framework 4.8 аҭахуп. Ишьақәыргыланы, Windows еиҭаҿашәкны, ашьақәыргылара еиҭаҿашәкы."
LangString DotNetRequired ${LANG_OSSETIAN} "Microsoft .NET Framework 4.8 хъæуы. Йæ сæвæр, Windows ногæй райсæр æмæ сæвæрд ногæй райсæр."
LangString DotNetRequired ${LANG_ARABIC} "مطلوب Microsoft .NET Framework 4.8. قم بتثبيته، وأعد تشغيل Windows، ثم قم بتشغيل Setup مرة أخرى."
LangString DotNetRequired ${LANG_HEBREW} "נדרש Microsoft .NET Framework 4.8. התקן אותו, הפעל מחדש את Windows והפעל שוב את Setup."
LangString DotNetRequired ${LANG_FARSI} "Microsoft .NET Framework 4.8 مورد نیاز است. آن را نصب کنید، Windows را مجددا راه اندازی کنید و دوباره Setup را اجرا کنید."
LangString DotNetRequired ${LANG_AFRIKAANS} "Microsoft .NET Framework 4.8 word vereis. Installeer dit, herbegin Windows en hardloop weer Setup."
LangString DotNetRequired ${LANG_HUNGARIAN} "Microsoft .NET Framework 4.8 szükséges. Telepítse, indítsa újra a Windows-t, és futtassa újra a Setup-t."
LangString DotNetRequired ${LANG_PORTUGUESE} "Microsoft .NET Framework 4.8 é necessário. Instale-o, reinicie o Windows e execute o Setup novamente."
LangString DotNetRequired ${LANG_CZECH} "Je vyžadován Microsoft .NET Framework 4.8. Nainstalujte jej, restartujte Windows a znovu spusťte Setup."
LangString DotNetRequired ${LANG_TURKISH} "Microsoft .NET Framework 4.8 gereklidir. Kurun, Windows'yi yeniden başlatın ve Setup'yi tekrar çalıştırın."
LangString DotNetRequired ${LANG_THAI} "ต้องใช้ Microsoft .NET Framework 4.8 ติดตั้ง รีสตาร์ท Windows และรัน Setup อีกครั้ง"
LangString DotNetRequired ${LANG_KOREAN} "Microsoft .NET Framework 4.8이 필요합니다. 이를 설치하고 Windows를 다시 시작한 후 Setup를 다시 실행하십시오."
LangString DotNetRequired ${LANG_JAPANESE} "Microsoft .NET Framework 4.8 が必要です。それをインストールし、Windows を再起動し、再び Setup を実行します。"
LangString DotNetRequired ${LANG_GEORGIAN} "Microsoft .NET Framework 4.8 საჭიროა. დააინსტალირეთ, გადატვირთეთ Windows და კვლავ გაუშვით Setup."
LangString DotNetRequired ${LANG_TRADCHINESE} "Microsoft .NET Framework 4.8 是必需的。安裝它，重新啟動Windows，然後再次運行Setup。"
LangString DotNetRequired ${LANG_NORWEGIANNYNORSK} "Microsoft .NET Framework 4.8 er påkrevd. Installer den, start Windows på nytt og kjør Setup igjen."
LangString DotNetRequired ${LANG_ITALIAN} "Microsoft .NET Framework 4.8. Installalo, riavvia Windows ed esegui nuovamente Setup."
LangString DotNetRequired ${LANG_ROMANIAN} "Este necesar Microsoft .NET Framework 4.8. Instalați-l, reporniți Windows și rulați Setup din nou."
LangString DotNetRequired ${LANG_ICELANDIC} "Microsoft .NET Framework 4.8 er krafist. Settu það upp, endurræstu Windows og keyrðu Setup aftur."
LangString DotNetRequired ${LANG_AZERBAIJANI} "Microsoft .NET Framework 4.8 tələb olunur. Onu quraşdırın, Windows-ni yenidən başladın və yenidən Setup-ni işə salın."
LangString DotNetRequired ${LANG_KYRGYZ} "Microsoft .NET Framework 4.8 талап кылынат. Аны орнотуп, Windows-ди кайра иштетиңиз жана Setup-ди кайра иштетиңиз."
LangString DotNetRequired ${LANG_UYGHURCYRILLIC} "Microsoft .NET Framework 4.8 тәләп қилиниду. уни қачилаң , Windows ни қайта қозғитип , Setup ни қайта иҗра қилиң."

Function .onInit
  !insertmacro MUI_LANGDLL_DISPLAY
  ${IfNot} ${AtLeastWin7}
    MessageBox MB_OK|MB_ICONSTOP "$(UnsupportedWindows)"
    Abort
  ${EndIf}
  ${If} ${RunningX64}
    SetRegView 64
    StrCpy $INSTDIR "$PROGRAMFILES64\ESD Installer Windows 7"
  ${Else}
    SetRegView 32
  ${EndIf}
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  IntCmp $0 528040 dotnet_ok dotnet_missing dotnet_ok
  dotnet_missing:
    MessageBox MB_OK|MB_ICONSTOP "$(DotNetRequired)"
    Abort
  dotnet_ok:
  SetShellVarContext all
FunctionEnd

Section "ESD Installer for Windows 7" SEC_MAIN
  SectionIn RO
  SetShellVarContext all
  SetOverwrite on
  SetOutPath "$INSTDIR"
  File /r /x "*.pdb" "${APP_SOURCE}\*.*"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\ESD Installer"
  CreateShortcut "$SMPROGRAMS\ESD Installer\ESD Installer for Windows 7.lnk" "$INSTDIR\ESDInstaller.Windows7.exe" "" "$INSTDIR\ESDInstaller.Windows7.exe" 0
  CreateShortcut "$DESKTOP\ESD Installer for Windows 7.lnk" "$INSTDIR\ESDInstaller.Windows7.exe" "" "$INSTDIR\ESDInstaller.Windows7.exe" 0
  WriteRegStr HKLM "${APP_REG_KEY}" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\ESDInstaller.Windows7.exe"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "UninstallString" "$\"$INSTDIR\Uninstall.exe$\""
  WriteRegDWORD HKLM "${APP_UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${APP_UNINSTALL_KEY}" "NoRepair" 1
  WriteRegDWORD HKLM "${APP_UNINSTALL_KEY}" "EstimatedSize" ${APP_SIZE_KB}
SectionEnd

Section "Uninstall"
  SetShellVarContext all
  Delete "$SMPROGRAMS\ESD Installer\ESD Installer for Windows 7.lnk"
  RMDir "$SMPROGRAMS\ESD Installer"
  Delete "$DESKTOP\ESD Installer for Windows 7.lnk"
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
