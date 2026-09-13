@echo off
chcp 65001 >nul
rem AutoDisplayPower lid detection test launcher
rem Usage:  lidtest.cmd open     /     lidtest.cmd closed
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0lidtest.ps1" %*
echo.
pause
