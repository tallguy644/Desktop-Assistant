@echo off
start /wait "" "%~dp0Syra.exe" --self-test
type "%TEMP%\Skynet-selftest.txt"
pause
