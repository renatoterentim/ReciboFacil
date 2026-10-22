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

rem --- Instaladores (precisam do Inno Setup 6; se nao estiver instalado, so pula) ---
for /f "delims=" %%V in ('powershell -NoProfile -ExecutionPolicy Bypass -File scripts\versao.ps1') do set VERSAO=%%V
set ISCC=
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe
if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe
if defined ISCC (
  echo.
  echo --- Gerando instaladores %VERSAO% ---
  "%ISCC%" /Qp /DArq=x64 /DVersao=%VERSAO% instalador\ReciboFacil.iss
  if errorlevel 1 exit /b 1
  "%ISCC%" /Qp /DArq=x86 /DVersao=%VERSAO% instalador\ReciboFacil.iss
  if errorlevel 1 exit /b 1
) else (
  echo.
  echo Inno Setup nao encontrado: instaladores nao gerados. Instale em https://jrsoftware.org/isdl.php
)
echo.
echo Pronto. Os arquivos estao nas pastas "publicar" e "instalador-saida".
