@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup.ps1"
if errorlevel 1 (echo Installation failed. & pause & exit /b 1)
echo Installation complete. Open Desktop Assistant from your Desktop.
pause
