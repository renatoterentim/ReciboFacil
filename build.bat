@echo off
setlocal
cd /d "%~dp0"
echo ==== ReciboFacil: compilando (Windows 64 e 32 bits) ====
for %%R in (win-x64 win-x86) do (
  echo.
  echo --- Publicando %%R ---
  dotnet publish ReciboFacil.Servidor\ReciboFacil.Servidor.csproj -c Release -r %%R --self-contained true -o publicar\%%R\Servidor
  if errorlevel 1 exit /b 1
  dotnet publish ReciboFacil.Cliente\ReciboFacil.Cliente.csproj -c Release -r %%R --self-contained true -o publicar\%%R\Cliente
  if errorlevel 1 exit /b 1
  copy /y scripts\*.bat publicar\%%R\ >nul
)
echo.
echo Pronto. Os arquivos estao na pasta "publicar".
