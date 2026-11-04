@echo off
setlocal
if /I not "%~d0"=="H:" (
    echo Run this file from the existing H:\WorldForge-Sim checkout.
    exit /b 1
)
rem Set before PowerShell starts; its module cache otherwise uses the user profile.
set "PSModuleAnalysisCachePath=nul"
rem The existing checkout is the bootstrap temp; PowerShell validates Temp before creating it.
set "TEMP=%~dp0"
set "TMP=%~dp0"
set "TMPDIR=%~dp0"
cd /d "%~dp0"
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-self-test.ps1"
set "self_test_exit=%errorlevel%"
echo.
echo Test finished. Read the report path above; paste the copied report in this conversation.
echo If copying failed, open REPORT.txt in the printed evidence folder.
pause
exit /b %self_test_exit%
