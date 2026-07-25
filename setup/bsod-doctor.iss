; BSOD Doctor — Inno Setup Script
; Derleme: iscc /dMyAppVersion="0.1-beta.X" /dMyAppBuildNumber=X setup\bsod-doctor.iss

#ifndef MyAppVersion
  #define MyAppVersion "0.1-beta"
#endif

#ifndef MyAppBuildNumber
  #define MyAppBuildNumber "0"
#endif

#define MyAppName "BSOD Doctor"
#define MyAppPublisher "NextroByte"
#define MyAppURL "https://github.com/burakdmrbkr/bsod_doctor"
#define MyAppExeName "BsodDoctor.exe"
#define MyServiceExeName "BsodDoctor.Service.exe"
#define MyAumid "NextroByte.BsodDoctor"
#define MyActivatorClsid "{B5E7F3A1-2C4D-4A8F-9E6B-1D3C5F7A9B0E}"

[Setup]
AppId={{B5E7F3A1-2C4D-4A8F-9E6B-1D3C5F7A9B0E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\artifacts
OutputBaseFilename=BSOD-Doctor-{#MyAppVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion=0.1.0.{#MyAppBuildNumber}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName}
ShowLanguageDialog=no
; Kurulum sonunda restart gerekebilir (service kaydı için)
RestartIfNeededByRun=no

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
; WPF uygulama dosyaları
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Windows Service dosyaları
Source: "..\publish-service\*"; DestDir: "{app}\service"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; COM activator CLSID kaydı (toast tıklama aktivasyonu için)
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{#MyActivatorClsid}"; ValueType: string; ValueName: ""; ValueData: "BsodDoctor Notification Activator"; Flags: deletekey
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{#MyActivatorClsid}\LocalServer32"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName} --notify"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{#MyActivatorClsid}\LocalServer32"; ValueType: string; ValueName: "AppID"; ValueData: "{#MyActivatorClsid}"; Flags: uninsdeletekey

; AppID kaydı — COM server'ın kullanıcı oturumunda çalışması için
Root: HKLM; Subkey: "SOFTWARE\Classes\AppID\{#MyActivatorClsid}"; ValueType: string; ValueName: ""; ValueData: "BsodDoctor"
Root: HKLM; Subkey: "SOFTWARE\Classes\AppID\{#MyActivatorClsid}"; ValueType: string; ValueName: "RunAs"; ValueData: "INTERACTIVE_USER"

; AUMID kaydı — Windows bildirim merkezi için
Root: HKLM; Subkey: "SOFTWARE\Classes\AppUserModelId\{#MyAumid}"; ValueType: string; ValueName: ""; ValueData: "BSOD Doctor"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\Classes\AppUserModelId\{#MyAumid}"; ValueType: string; ValueName: "DisplayName"; ValueData: "BSOD Doctor"
Root: HKLM; Subkey: "SOFTWARE\Classes\AppUserModelId\{#MyAumid}"; ValueType: string; ValueName: "IconUri"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKLM; Subkey: "SOFTWARE\Classes\AppUserModelId\{#MyAumid}"; ValueType: string; ValueName: "IconBackgroundColor"; ValueData: "transparent"

[Run]
; Windows Service'i kur ve başlat
Filename: "sc"; Parameters: "create BsodDoctorService binPath=""{app}\service\{#MyServiceExeName}"" start=auto"; Flags: runhidden
Filename: "sc"; Parameters: "description BsodDoctorService ""BSOD Doctor — Minidump tarama ve bildirim servisi"""; Flags: runhidden
Filename: "net"; Parameters: "start BsodDoctorService"; Flags: runhidden

; WPF uygulamasını kullanıcı girişinde --notify modunda başlatmak için auto-start kaydı
Filename: "reg"; Parameters: "add HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v BsodDoctor /t REG_SZ /d ""{app}\{#MyAppExeName} --notify"" /f"; Flags: runhidden

; Start Menu kısayolunu AUMID ile oluştur (toast notification'lar için gerekli)
Filename: "{app}\{#MyAppExeName}"; Parameters: "--install-shortcut"; Flags: runhidden nowait

; Uygulamayı başlat (isteğe bağlı)
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Windows Service'i durdur ve kaldır
Filename: "net"; Parameters: "stop BsodDoctorService"; Flags: runhidden; RunOnceId: "StopService"
Filename: "sc"; Parameters: "delete BsodDoctorService"; Flags: runhidden; RunOnceId: "DeleteService"

; Auto-start kaydını temizle
Filename: "reg"; Parameters: "delete HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v BsodDoctor /f"; Flags: runhidden

[Code]
function IsDotNetRuntimeInstalled: Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('cmd.exe', '/C "dotnet --list-runtimes | findstr /C:"Microsoft.WindowsDesktop.App 10." > nul 2>&1"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  if Result then
    Result := ResultCode = 0;
end;

function InitializeSetup: Boolean;
var
  ResultCode: Integer;
begin
  if not IsDotNetRuntimeInstalled then
  begin
    if MsgBox('{#MyAppName} calismasi icin .NET 10 Desktop Runtime gereklidir.'#13#13
              'Su an yuklemek ister misiniz?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/en-us/download/dotnet/10.0', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
      Result := False;
    end
    else
      Result := False;
  end
  else
    Result := True;
end;
