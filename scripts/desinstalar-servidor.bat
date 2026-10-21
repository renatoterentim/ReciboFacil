@echo off
net session >nul 2>&1
if errorlevel 1 (
  echo Clique com o botao direito neste arquivo e escolha "Executar como administrador".
  pause
  exit /b 1
)
sc stop ReciboFacilServidor
sc delete ReciboFacilServidor
netsh advfirewall firewall delete rule name="ReciboFacil Servidor"
echo.
echo Servico removido. Os dados em C:\ReciboFacil\Dados NAO foram apagados.
pause
