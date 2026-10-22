; ReciboFacil - instalador (Inno Setup 6.3 ou mais novo)
; Compilar:  ISCC /DArq=x64 /DVersao=2.0.0 instalador\ReciboFacil.iss   (ou x86)
; Entrada: pasta "publicar\win-x64" (ou win-x86) gerada pelo build.bat

#ifndef Arq
  #define Arq "x64"
#endif
#ifndef Versao
  #define Versao "2.0.0"
#endif

#define AppNome "ReciboFácil"
#define AppId "{{6F0C3B52-8E1D-4C7A-9B7E-5D2A41C8E3F9}"
#define ServicoNome "ReciboFacilServidor"

[Setup]
AppId={#AppId}
AppName={#AppNome}
AppVersion={#Versao}
AppPublisher=Renato Terentim
DefaultDirName=C:\ReciboFacil
DefaultGroupName={#AppNome}
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
PrivilegesRequired=admin
OutputDir=..\instalador-saida
OutputBaseFilename=ReciboFacil-Setup-{#Versao}-{#Arq}
SetupIconFile=..\ReciboFacil.Cliente\icone.ico
UninstallDisplayIcon={app}\Cliente\ReciboFacil.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
#if Arq == "x64"
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Types]
Name: "servidor"; Description: "Computador SERVIDOR (guarda os dados e também usa o programa)"
Name: "cliente"; Description: "Computador comum (só o programa, conecta ao servidor)"
Name: "custom"; Description: "Personalizada"; Flags: iscustom

[Components]
Name: "servidor"; Description: "Servidor de dados (serviço do Windows)"; Types: servidor custom
Name: "cliente"; Description: "Programa ReciboFácil"; Types: servidor cliente custom; Flags: fixed

[Tasks]
Name: "atalhodesktop"; Description: "Criar atalho na área de trabalho"; Components: cliente

[Dirs]
Name: "{app}\Dados"; Components: servidor; Flags: uninsneveruninstall

[Files]
; appsettings.json só é gravado se ainda não existir (instalações antigas guardam o caminho dos dados nele).
Source: "..\publicar\win-{#Arq}\Servidor\*"; DestDir: "{app}\Servidor"; Excludes: "appsettings.json"; Components: servidor; Flags: recursesubdirs ignoreversion
Source: "..\publicar\win-{#Arq}\Servidor\appsettings.json"; DestDir: "{app}\Servidor"; Components: servidor; Flags: onlyifdoesntexist
Source: "..\publicar\win-{#Arq}\Cliente\*"; DestDir: "{app}\Cliente"; Components: cliente; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autoprograms}\{#AppNome}"; Filename: "{app}\Cliente\ReciboFacil.exe"; Components: cliente
Name: "{autoprograms}\{#AppNome} - Restaurar backup"; Filename: "{app}\Cliente\ReciboFacil.exe"; Parameters: "--restaurar"; Components: servidor
Name: "{autodesktop}\{#AppNome}"; Filename: "{app}\Cliente\ReciboFacil.exe"; Tasks: atalhodesktop; Components: cliente

[Run]
Filename: "{app}\Cliente\ReciboFacil.exe"; Description: "Abrir o {#AppNome} agora"; Flags: postinstall nowait skipifsilent runasoriginaluser; Components: cliente

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop {#ServicoNome}"; Flags: runhidden; RunOnceId: "ParaServico"
Filename: "{sys}\sc.exe"; Parameters: "delete {#ServicoNome}"; Flags: runhidden; RunOnceId: "ApagaServico"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""ReciboFacil Servidor"""; Flags: runhidden; RunOnceId: "ApagaFirewall"

[Code]
var
  Atualizando: Boolean;

function InstalacaoExistente(): Boolean;
begin
  Result :=
    RegKeyExists(HKLM, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{6F0C3B52-8E1D-4C7A-9B7E-5D2A41C8E3F9}_is1') or
    FileExists('C:\ReciboFacil\Cliente\ReciboFacil.exe') or
    FileExists('C:\ReciboFacil\Servidor\ReciboFacil.Servidor.exe');
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  Atualizando := InstalacaoExistente();
  if Atualizando and not WizardSilent() then
  begin
    Result := MsgBox(
      'Já existe uma instalação do {#AppNome} neste computador.' + #13#10 + #13#10 +
      'Deseja ATUALIZAR para a versão {#Versao}?' + #13#10 + #13#10 +
      'Seus recibos, contatos e usuários serão mantidos. Antes de atualizar, será feita uma cópia de segurança do banco de dados.',
      mbConfirmation, MB_YESNO) = IDYES;
  end;
end;

function Executar(const Arquivo, Parametros: String): Integer;
var
  Codigo: Integer;
begin
  if not Exec(Arquivo, Parametros, '', SW_HIDE, ewWaitUntilTerminated, Codigo) then
    Codigo := -1;
  Result := Codigo;
end;

// Para o serviço e espera ele encerrar de verdade (até ~30 s). Se não encerrar, finaliza o processo.
procedure PararServidor();
var
  i: Integer;
  Saida: Integer;
begin
  Executar(ExpandConstant('{sys}\sc.exe'), 'stop {#ServicoNome}');
  for i := 1 to 30 do
  begin
    Sleep(1000);
    // sc query devolve 0 mesmo parado; por isso conferimos se o .exe ainda está em execução
    if Executar(ExpandConstant('{sys}\cmd.exe'),
        '/c tasklist /fi "imagename eq ReciboFacil.Servidor.exe" | find /i "ReciboFacil.Servidor.exe"') <> 0 then
      Exit;
  end;
  Executar(ExpandConstant('{sys}\taskkill.exe'), '/f /im ReciboFacil.Servidor.exe');
  Sleep(1000);
end;

// Chamado antes da verificação de arquivos em uso: com o servidor já parado, o instalador não pede para fechar nada.
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if WizardIsComponentSelected('servidor') then PararServidor();
end;

procedure CopiaBanco();
var
  Db, Pasta: String;
begin
  Db := ExpandConstant('{app}\Dados\recibofacil.db');
  if not FileExists(Db) then Exit;
  Pasta := ExpandConstant('{app}\Dados\Backups');
  ForceDirectories(Pasta);
  FileCopy(Db, Pasta + '\pre-atualizacao-' + GetDateTimeString('yyyymmdd-hhnnss', '-', ':') + '.db', False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Exe: String;
begin
  if not WizardIsComponentSelected('servidor') then Exit;

  if CurStep = ssInstall then
  begin
    PararServidor();
    CopiaBanco();
  end;

  if CurStep = ssPostInstall then
  begin
    Exe := ExpandConstant('{app}\Servidor\ReciboFacil.Servidor.exe');
    if Executar(ExpandConstant('{sys}\sc.exe'), 'query {#ServicoNome}') <> 0 then
      Executar(ExpandConstant('{sys}\sc.exe'),
        'create {#ServicoNome} binPath= "\"' + Exe + '\"" start= auto DisplayName= "ReciboFacil Servidor"')
    else
      Executar(ExpandConstant('{sys}\sc.exe'),
        'config {#ServicoNome} binPath= "\"' + Exe + '\"" start= auto');
    Executar(ExpandConstant('{sys}\sc.exe'), 'description {#ServicoNome} "Servidor de dados do ReciboFacil"');
    Executar(ExpandConstant('{sys}\sc.exe'), 'failure {#ServicoNome} reset= 86400 actions= restart/5000/restart/5000/restart/5000');
    Executar(ExpandConstant('{sys}\netsh.exe'), 'advfirewall firewall delete rule name="ReciboFacil Servidor"');
    Executar(ExpandConstant('{sys}\netsh.exe'), 'advfirewall firewall add rule name="ReciboFacil Servidor" dir=in action=allow protocol=TCP localport=5180');
    Executar(ExpandConstant('{sys}\sc.exe'), 'start {#ServicoNome}');
  end;
end;
