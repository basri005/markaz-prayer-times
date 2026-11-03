#define AppVersion "2.0.1"
[Setup]
AppId={{E07614A2-E43D-4EDC-922E-FAD2DB6A93BA}
AppName=Prayer Times
AppVersion={#AppVersion}
AppPublisher=Prayer Times community project
DefaultDirName={localappdata}\Programs\MarkazPrayerTimes
DefaultGroupName=Prayer Times
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir=..\dist
OutputBaseFilename=Prayer-Times-Setup-{#AppVersion}
SetupIconFile=..\assets\prayer-times.ico
UninstallDisplayIcon={app}\Prayer Times.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
DisableProgramGroupPage=yes

[Tasks]
Name: startup; Description: "Start quietly when I sign in to Windows"; Flags: checkedonce
Name: idle; Description: "Display prayer times automatically when idle"; Flags: checkedonce
Name: mains; Description: "Only start the automatic idle display while plugged in"; Flags: checkedonce
Name: automatic; Description: "Check the masjid website for new monthly timetables"; Flags: checkedonce
Name: desktop; Description: "Create a desktop shortcut"; Flags: checkedonce
Name: pinhelp; Description: "Show me how to pin Prayer Times to the taskbar"; Flags: checkedonce

[Files]
Source: "..\build\Prayer Times.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\build\Tesseract.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\build\x64\*.dll"; DestDir: "{app}\x64"; Flags: ignoreversion
Source: "..\build\tessdata\eng.traineddata"; DestDir: "{app}\tessdata"; Flags: ignoreversion
Source: "..\assets\timetable.csv"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion
Source: "..\dependencies\vc_redist.x64.exe"; Flags: dontcopy

[Icons]
Name: "{group}\Prayer Times"; Filename: "{app}\Prayer Times.exe"
Name: "{group}\Uninstall Prayer Times"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Prayer Times"; Filename: "{app}\Prayer Times.exe"; Tasks: desktop

[UninstallRun]
Filename: "{app}\Prayer Times.exe"; Parameters: "--disable-startup"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveStartup"

[Run]
Filename: "{app}\Prayer Times.exe"; Parameters: "--background"; Description: "Start Prayer Times"; Flags: nowait postinstall skipifsilent
Filename: "{win}\explorer.exe"; Parameters: "/select,""{app}\Prayer Times.exe"""; Description: "Open Prayer Times so I can pin it to the taskbar"; Flags: nowait postinstall skipifsilent; Tasks: pinhelp

[Code]
var IdlePage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  IdlePage := CreateInputQueryPage(wpSelectTasks, 'Idle display delay', 'When should the timetable appear?', 'Enter the number of minutes without mouse or keyboard input (1 to 120). You can change this later in the tray menu Settings.');
  IdlePage.Add('Minutes:', False);
  IdlePage.Values[0] := '5';
  WizardForm.FinishedLabel.Caption := 'Prayer Times is ready. To pin it, right-click Prayer Times.exe in the folder that opens, choose Show more options, then Pin to taskbar. Windows asks you to perform this final step.';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var Minutes: Integer;
begin
  Result := True;
  if CurPageID = IdlePage.ID then begin
    Minutes := StrToIntDef(IdlePage.Values[0], 0);
    if (Minutes < 1) or (Minutes > 120) then begin MsgBox('Choose a delay from 1 to 120 minutes.', mbError, MB_OK); Result := False; end;
  end;
end;

function NeedsVCRuntime: Boolean;
var Installed: Cardinal;
begin
  Result := not RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Installed);
  if not Result then Result := Installed <> 1;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Code: Integer;
begin
  Result := '';
  if NeedsVCRuntime then begin
    ExtractTemporaryFile('vc_redist.x64.exe');
    if not Exec(ExpandConstant('{tmp}\vc_redist.x64.exe'), '/install /passive /norestart', '', SW_SHOW, ewWaitUntilTerminated, Code) then
      Result := 'The Microsoft Visual C++ x64 runtime is needed for image reading. Please install it and run setup again.'
    else if (Code <> 0) and (Code <> 3010) and (Code <> 1638) then
      Result := 'Microsoft runtime installation was not completed (code ' + IntToStr(Code) + '). Please run setup again after installing the Microsoft Visual C++ x64 runtime.';
    if Code = 3010 then NeedsRestart := True;
  end;
end;

function JsonBool(Task: String): String;
begin
  if WizardIsTaskSelected(Task) then Result := 'true' else Result := 'false';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var DataPath, Config, StartupArgument: String; StartupCode: Integer;
begin
  if CurStep = ssPostInstall then begin
    DataPath := ExpandConstant('{localappdata}\MarkazPrayerTimes\Data'); ForceDirectories(DataPath);
    Config := '{"IdleEnabled":' + JsonBool('idle') + ',"MainsOnly":' + JsonBool('mains') + ',"AutoCheck":' + JsonBool('automatic') + ',"IdleMinutes":' + IdlePage.Values[0] + '}';
    SaveStringToFile(DataPath + '\settings.json', Config, False);
    if WizardIsTaskSelected('startup') then StartupArgument := '--enable-startup' else StartupArgument := '--disable-startup';
    if not Exec(ExpandConstant('{app}\Prayer Times.exe'), StartupArgument, ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, StartupCode) then
      MsgBox('Windows startup could not be configured. Open Prayer Times settings and enable Start quietly when I sign in to Windows.', mbError, MB_OK)
    else if StartupCode <> 0 then
      MsgBox('Windows startup could not be configured. Open Prayer Times settings and enable Start quietly when I sign in to Windows.', mbError, MB_OK);
  end;
end;
