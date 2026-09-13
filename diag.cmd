@echo off
chcp 65001 >nul
rem === AutoDisplayPower diagnostic launcher (ASCII only on purpose) ===
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0diag.ps1"
echo.
pause
