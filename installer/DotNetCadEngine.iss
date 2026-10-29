; =====================================================================
; Inno Setup 6 Script para DotNetCadEngine (CAD 2D Técnico Windows x64)
; =====================================================================

#define MyAppName "DotNetCadEngine"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "DotNetCadEngine Contributors"
#define MyAppURL "https://github.com/led-21/DotNetCadEngine"
#define MyAppExeName "DotNetCad.Demo.exe"
#define MyAppId "{D5C8A714-3F21-4E89-B09A-4E38F9B6D211}"

[Setup]
; Identidade estável da aplicação
AppId={{#MyAppId}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases

; Instalação por usuário sem necessidade de elevação UAC por padrão
; Permite ao usuário escolher instalação por máquina se desejar
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Detecção de processo em execução (mutex) e solicitação de fechamento limpo
AppMutex=DotNetCadEngine_Running_Mutex
CloseApplications=yes
CloseApplicationsFilter=*.exe
RestartApplications=no

; Configuração de arquitetura x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Configurações visuais e identidade técnica
WizardStyle=modern
WizardImageFile=assets\wizard-banner.bmp
WizardSmallImageFile=assets\wizard-small.bmp
SetupIconFile=..\src\DotNetCad.Demo\Assets\dotnetcad.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Licença
LicenseFile=LICENSE-PRO.txt

; Saída do instalador
OutputDir=..\artifacts\installer
OutputBaseFilename=DotNetCadEngine-{#MyAppVersion}-Setup-x64
Compression=lzma2/ultra64
SolidCompression=yes

; Atualização e desinstalação
DisableDirPage=no
DisableProgramGroupPage=yes
UsedUserAreasWarning=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
brazilianportuguese.CreateDesktopIcon=Criar atalho na Área de Trabalho
brazilianportuguese.AssociateCadFiles=Associar arquivos de desenho .cad ao DotNetCadEngine
brazilianportuguese.CadFileDescription=Desenho DotNetCad (*.cad)
brazilianportuguese.LaunchProgram=Executar o DotNetCadEngine agora

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "associatecad"; Description: "{cm:AssociateCadFiles}"; GroupDescription: "Associações de Arquivo:"; Flags: checkedonce

[Files]
; Binários publicados (self-contained win-x64)
Source: "..\artifacts\publish\x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Atalhos no Menu Iniciar
Name: "{autoprograms}\{#MyAppName}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autoprograms}\{#MyAppName}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"
; Atalho opcional na Área de Trabalho
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\{#MyAppExeName}"

[Registry]
; Associação do formato nativo .cad (respeita se a task estiver desmarcada)
Root: HKA; Subkey: "Software\Classes\.cad"; ValueType: string; ValueName: ""; ValueData: "DotNetCad.Drawing"; Flags: uninsdeletevalue; Tasks: associatecad
Root: HKA; Subkey: "Software\Classes\DotNetCad.Drawing"; ValueType: string; ValueName: ""; ValueData: "{cm:CadFileDescription}"; Flags: uninsdeletekey; Tasks: associatecad
Root: HKA; Subkey: "Software\Classes\DotNetCad.Drawing\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey; Tasks: associatecad
Root: HKA; Subkey: "Software\Classes\DotNetCad.Drawing\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: associatecad

[Run]
; Executar aplicação ao final (sem privilégios elevados herdados)
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Não apaga pastas ou desenhos do usuário; apenas limpa arquivos temporários do diretório do app
Type: files; Name: "{app}\*.log"

[Code]
// Procedimentos adicionais de validação se necessário
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
