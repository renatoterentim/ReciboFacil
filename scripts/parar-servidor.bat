@echo off
rem Execute como Administrador. Use antes de copiar uma versao nova do programa para o servidor.
net session >nul 2>&1
if errorlevel 1 (
  echo Clique com o botao direito neste arquivo e escolha "Executar como administrador".
  pause
  exit /b 1
)
sc stop ReciboFacilServidor
echo.
echo Servico parado. Copie os arquivos novos e depois execute instalar-servidor.bat como administrador.
pause
