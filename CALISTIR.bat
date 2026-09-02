@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo === SwPdm calistiriliyor ===
dotnet run --project src\SwPdm.Arayuz
echo.
echo === Cikis kodu: %ERRORLEVEL% ===
pause
