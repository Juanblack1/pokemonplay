#ifndef RuntimeDirectory
  #error RuntimeDirectory is required
#endif
#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef InstallerOutputDirectory
  #error InstallerOutputDirectory is required
#endif
#define RuntimeExe AddBackslash(RuntimeDirectory) + "Pokemons Play.exe"
#ifndef VersionMS
  #error VersionMS is required
#endif
#ifndef VersionLS
  #error VersionLS is required
#endif

[Setup]
AppId={{AE7A3D84-8D30-4655-B635-CE5F6D1842EC}
AppName=Pokémon Play
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\PokemonPlay
DisableDirPage=yes
DefaultGroupName=Pokémon Play
DisableProgramGroupPage=yes
UsePreviousAppDir=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#InstallerOutputDirectory}
OutputBaseFilename=pokemon-play-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\PokemonPlayRuntime\Pokemons Play.exe
CloseApplications=no
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; Flags: unchecked

[Files]
Source: "{#RuntimeDirectory}\*"; DestDir: "{app}\PokemonPlayRuntime"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Pokémon Play"; Filename: "{app}\PokemonPlayRuntime\Pokemons Play.exe"; WorkingDir: "{app}\PokemonPlayRuntime"
Name: "{userdesktop}\Pokémon Play"; Filename: "{app}\PokemonPlayRuntime\Pokemons Play.exe"; WorkingDir: "{app}\PokemonPlayRuntime"; Tasks: desktopicon

[Run]
Filename: "{app}\PokemonPlayRuntime\Pokemons Play.exe"; Description: "Abrir Pokémon Play"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; The updater can introduce files absent from the original uninstall log.
; Personal data lives beside this managed runtime and must never be removed.
Type: filesandordirs; Name: "{app}\PokemonPlayRuntime"

[Code]
function CreateFileW(FileName: String; Access, Share: Cardinal; Security: Integer; Creation, Flags: Cardinal; Template: Integer): Integer; external 'CreateFileW@kernel32.dll stdcall';
function CloseHandle(Handle: Integer): Boolean; external 'CloseHandle@kernel32.dll stdcall';
function RuntimeFilesAvailable(const Directory: String): Boolean;
var
  Entry: TFindRec;
  Handle: Integer;
  Path: String;
begin
  Result := True;
  if FindFirst(Directory + '\*', Entry) then begin
    try
      repeat
        if (Entry.Name <> '.') and (Entry.Name <> '..') then begin
          Path := Directory + '\' + Entry.Name;
          if (Entry.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
            Result := RuntimeFilesAvailable(Path)
          else begin
            Handle := CreateFileW(Path, $80000000, 0, 0, 3, $80, 0);
            Result := Handle <> -1;
            if Handle <> -1 then CloseHandle(Handle);
          end;
          if not Result then Break;
        end;
      until not FindNext(Entry);
    finally
      FindClose(Entry);
    end;
  end;
end;

function ApplicationProcessesStopped: Boolean;
var
  Locator, Service, Processes, Process, ExecutablePath: Variant;
  I: Integer;
  Root, Path: String;
begin
  Result := False;
  Root := Lowercase(AddBackslash(ExpandConstant('{app}')));
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Service := Locator.ConnectServer('.', 'root\CIMV2');
    Processes := Service.ExecQuery('SELECT ExecutablePath FROM Win32_Process');
    for I := 0 to Processes.Count - 1 do begin
      Process := Processes.ItemIndex(I);
      ExecutablePath := Process.ExecutablePath;
      if not VarIsNull(ExecutablePath) then begin
        Path := ExecutablePath;
        Path := Lowercase(Path);
        if (Pos(Root, Path) = 1) and
           (Path <> Lowercase(ExpandConstant('{uninstallexe}'))) then Exit;
      end;
    end;
    Result := RuntimeFilesAvailable(ExpandConstant('{app}\PokemonPlayRuntime'));
  except
    Log('Unable to verify running processes: ' + GetExceptionMessage);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExistingMS, ExistingLS: Cardinal;
  ExistingExe: String;
begin
  Result := '';
  ExistingExe := ExpandConstant('{app}\PokemonPlayRuntime\Pokemons Play.exe');
  if FileExists(ExistingExe) then begin
    if not GetVersionNumbers(ExistingExe, ExistingMS, ExistingLS) then begin
      Result := 'Não foi possível verificar a versão instalada. A instalação foi interrompida.';
      Exit;
    end;
    if (ExistingMS > {#VersionMS}) or
       ((ExistingMS = {#VersionMS}) and (ExistingLS > {#VersionLS})) then begin
      Result := 'Uma versão mais recente do Pokémon Play já está instalada. Use o atualizador do aplicativo.';
      Exit;
    end;
  end;
  if not ApplicationProcessesStopped then
    Result := 'Feche o Pokémon Play e seus emuladores antes de instalar. Não foi possível confirmar que os arquivos estão livres.';
end;

function InitializeUninstall: Boolean;
begin
  Result := ApplicationProcessesStopped;
  if not Result then
    SuppressibleMsgBox('Feche o Pokémon Play e seus emuladores antes de desinstalar. Seus dados pessoais serão preservados.', mbError, MB_OK, IDOK);
end;






