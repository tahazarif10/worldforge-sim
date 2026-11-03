@echo off
setlocal
cd /d "%~dp0"
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-self-test.ps1"
set "self_test_exit=%errorlevel%"
echo.
echo Test finished. Read the report path above; paste the copied report in this conversation.
echo If copying failed, open REPORT.txt in the printed evidence folder.
pause
exit /b %self_test_exit%
