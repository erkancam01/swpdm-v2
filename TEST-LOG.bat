@echo off
chcp 65001 >nul
cd /d "%~dp0"
dotnet test testler\SwPdm.Cekirdek.Testler --nologo > test-log.txt 2>&1
echo bitti
