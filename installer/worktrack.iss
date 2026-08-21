; ─────────────────────────────────────────────────────────────────────────
; WorkTrack Lite Agent — Inno Setup Script
;
; Menginstal:
;   - WorkTrack.Service.exe  → Windows Service ("WorkTrackAgent"), berjalan
;     di Session 0 sejak boot (Local System), auto-start.
;   - WorkTrack.SessionAgent.exe → disalin sebagai dependency di direktori
;     yang sama; diluncurkan oleh Service ke sesi interaktif user
;     (lihat WorkTrack.Service/SessionLauncher.cs).
;
; Build output yang dibutuhkan sebelum compile installer ini:
;   dotnet publish agent/WorkTrack.Service/WorkTrack.Service.csproj ^
;       -c Release -r win-x64 --self-contained false -o installer/publish/service
;   dotnet publish agent/WorkTrack.SessionAgent/WorkTrack.SessionAgent.csproj ^
;       -c Release -r win-x64 --self-contained false -o installer/publish/sessionagent
;
; Compile: iscc installer/worktrack.iss
; ─────────────────────────────────────────────────────────────────────────

#define MyAppName "WorkTrack Agent"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "WorkTrack Lite"
#define MyServiceName "WorkTrackAgent"
#define MyServiceDisplayName "WorkTrack Agent"

[Setup]
AppId={{8F3B2E1A-6C4D-4A9E-9F2B-1D7E5C8A9B01}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\WorkTrack
DefaultGroupName=WorkTrack
DisableProgramGroupPage=yes
DisableDirPage=yes
DisableWelcomePage=no
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=WorkTrackAgentSetup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\WorkTrack.Service.exe
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Windows Service host
Source: "publish\service\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; SessionAgent (screenshot/idle/foreground capture) — dependency, disalin ke direktori yang sama
Source: "publish\sessionagent\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Code]
var
  ServerUrlPage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  ServerUrlPage := CreateInputQueryPage(wpSelectDir,
    'Konfigurasi Server', 'Alamat server WorkTrack Lite',
    'Masukkan URL server API tempat agent ini akan mengirim data (mis. https://worktrack.perusahaan.local).');
  ServerUrlPage.Add('Server URL:', False);
  ServerUrlPage.Values[0] := 'https://localhost:7000';
end;

function GetServerUrl(Param: string): string;
begin
  Result := ServerUrlPage.Values[0];
end;

[Run]
; Tulis Agent:ServerUrl ke appsettings.json via dotnet config tidak praktis di Inno,
; sehingga kita override melalui environment variable yang dibaca .NET Configuration
; (ASPNETCORE-style: Agent__ServerUrl) saat service dijalankan sebagai Local System.
Filename: "{sys}\sc.exe"; \
    Parameters: "create ""{#MyServiceName}"" binPath= ""\""{app}\WorkTrack.Service.exe\"""" start= auto DisplayName= ""{#MyServiceDisplayName}"""; \
    Flags: runhidden waituntilterminated; StatusMsg: "Mendaftarkan Windows Service..."

Filename: "{sys}\sc.exe"; \
    Parameters: "description ""{#MyServiceName}"" ""Agen pemantauan aktivitas WorkTrack Lite (foreground app, idle, screenshot)."""; \
    Flags: runhidden waituntilterminated

Filename: "{sys}\reg.exe"; \
    Parameters: "add ""HKLM\SYSTEM\CurrentControlSet\Services\{#MyServiceName}"" /v Environment /t REG_MULTI_SZ /d ""Agent__ServerUrl={code:GetServerUrl}"" /f"; \
    Flags: runhidden waituntilterminated; StatusMsg: "Menyimpan konfigurasi server..."

Filename: "{sys}\sc.exe"; \
    Parameters: "start ""{#MyServiceName}"""; \
    Flags: runhidden waituntilterminated; StatusMsg: "Menjalankan service..."

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop ""{#MyServiceName}"""; Flags: runhidden waituntilterminated; RunOnceId: "StopService"
Filename: "{sys}\sc.exe"; Parameters: "delete ""{#MyServiceName}"""; Flags: runhidden waituntilterminated; RunOnceId: "DeleteService"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
