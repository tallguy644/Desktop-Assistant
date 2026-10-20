@echo off
start /wait "" "%~dp0Skynet.exe" --self-test
type "%TEMP%\Skynet-selftest.txt"
pause
