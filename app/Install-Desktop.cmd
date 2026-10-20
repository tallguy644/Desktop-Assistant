@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup.ps1"
if errorlevel 1 (echo Installation failed. Close the assistant before installing. & pause & exit /b 1)
start "" "%LOCALAPPDATA%\JarvisDesk\bin\Skynet.exe" --desktop
