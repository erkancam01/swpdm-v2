@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo ===== DERLEME =====
dotnet build SwPdm.sln -warnaserror --nologo
if errorlevel 1 goto son
echo.
echo ===== TESTLER =====
dotnet test testler\SwPdm.Cekirdek.Testler --nologo
echo.
echo ===== UYGULAMA ACILIYOR =====
start "" "src\SwPdm.Arayuz\bin\Debug\net8.0-windows\SwPdm.exe"
:son
echo.
echo ===== BITTI (kod %ERRORLEVEL%) =====
pause
