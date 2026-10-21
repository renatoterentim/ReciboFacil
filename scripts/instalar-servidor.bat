@echo off
rem Execute como Administrador, no computador que sera o servidor.
rem Coloque esta pasta em C:\ReciboFacil (com as subpastas Servidor e Cliente).
net session >nul 2>&1
if errorlevel 1 (
  echo Clique com o botao direito neste arquivo e escolha "Executar como administrador".
  pause
  exit /b 1
)
set EXE=%~dp0Servidor\ReciboFacil.Servidor.exe
if not exist "%EXE%" (
  echo Nao encontrei %EXE%
  pause
  exit /b 1
)
sc stop ReciboFacilServidor >nul 2>&1
sc delete ReciboFacilServidor >nul 2>&1
sc create ReciboFacilServidor binPath= "\"%EXE%\"" start= auto DisplayName= "ReciboFacil Servidor"
sc description ReciboFacilServidor "Servidor de dados do ReciboFacil"
sc failure ReciboFacilServidor reset= 86400 actions= restart/5000/restart/5000/restart/5000
netsh advfirewall firewall delete rule name="ReciboFacil Servidor" >nul 2>&1
netsh advfirewall firewall add rule name="ReciboFacil Servidor" dir=in action=allow protocol=TCP localport=5180
sc start ReciboFacilServidor
echo.
echo Servidor instalado e iniciado (porta 5180). Os dados ficam em C:\ReciboFacil\Dados
pause
